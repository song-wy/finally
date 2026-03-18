using System;
using System.Linq;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DiabetesPatientApp.Services
{
	public class DataSeedingService
	{
		private readonly DiabetesDbContext _context;
		private readonly IAuthService _authService;

		public DataSeedingService(DiabetesDbContext context, IAuthService authService)
		{
			_context = context;
			_authService = authService;
		}

		public async Task SeedAsync()
		{
			await _context.Database.EnsureCreatedAsync();
			await EnsureUserProfileColumnsAsync();
			await EnsureAdminEntryLogsTableAsync();
			await EnsureSystemSettingsTableAsync();
			await EnsureMedicalExperienceFeedbackTableAsync();
			await EnsureDoctorCustomGroupTablesAsync();
			await EnsureFollowUpTableAsync();
			await EnsureFollowUpReminderNotificationsTableAsync();
			await EnsureDoctorOrderTablesAsync();
			await EnsureEducationResourcesTableAsync();
			await EnsureQuestionnaireAssignmentsTableAsync();
			await EnsureDoctorHiddenTablesAsync();
			await EnsureHighRiskAlertNotificationsTableAsync();
			await EnsureOtherDepartmentDoctorsTableAsync();
			await SeedOtherDepartmentDoctorsAsync();

			// 仅保留“王”医生，删除其余医生（一次性清理）
			await RemoveDoctorsExceptWangAsync();

			// 创建默认管理员账号（如不存在）
			var adminExists = await _context.Users.AnyAsync(u =>
				u.Username == "admin" || (u.UserType == "Admin" && u.Email == "admin@local"));

			if (!adminExists)
			{
				var admin = new User
				{
					Username = "admin",
					Email = "admin@local",
					PasswordHash = _authService.HashPassword("Admin@123"),
					FullName = "系统管理员",
					UserType = "Admin",
					CreatedDate = DateTime.Now,
					IsActive = true
				};
				_context.Users.Add(admin);
				await _context.SaveChangesAsync();
			}
		}

		/// <summary>
		/// 删除除“王”医生以外的所有医生及其关联数据。
		/// </summary>
		private async Task RemoveDoctorsExceptWangAsync()
		{
			var wang = await _context.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(u => u.UserType == "Doctor" && (u.FullName == "王" || u.Username == "王"));
			if (wang == null)
				return;

			var toRemove = await _context.Users
				.Where(u => u.UserType == "Doctor" && u.UserId != wang.UserId)
				.Select(u => u.UserId)
				.ToListAsync();
			if (toRemove.Count == 0)
				return;

			foreach (var doctorId in toRemove)
			{
				await _context.DoctorHiddenFollowUpRecords.Where(x => x.DoctorId == doctorId).ExecuteDeleteAsync();
				await _context.FollowUpRecords.Where(f => f.DoctorId == doctorId).ExecuteDeleteAsync();
				await _context.DoctorHiddenHealthItems.Where(x => x.DoctorId == doctorId).ExecuteDeleteAsync();
				await _context.DoctorHiddenConsultationMessages.Where(x => x.DoctorId == doctorId).ExecuteDeleteAsync();
				await _context.ConsultationMessages.Where(m => m.SenderId == doctorId || m.ReceiverId == doctorId).ExecuteDeleteAsync();
				await _context.QuestionnaireAssignments.Where(q => q.DoctorId == doctorId).ExecuteDeleteAsync();

				var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == doctorId);
				if (user != null)
				{
					_context.Users.Remove(user);
				}
			}
			await _context.SaveChangesAsync();
		}

		private async Task EnsureFollowUpTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS FollowUpRecords (
    FollowUpRecordId INTEGER NOT NULL CONSTRAINT PK_FollowUpRecords PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    FollowUpDate TEXT NOT NULL,
    FollowUpMethod TEXT NOT NULL,
    Summary TEXT NOT NULL,
    Advice TEXT NULL,
    NextFollowUpDate TEXT NULL,
    CreatedDate TEXT NOT NULL,
    CONSTRAINT FK_FollowUpRecords_Users_DoctorId FOREIGN KEY (DoctorId) REFERENCES Users (UserId),
    CONSTRAINT FK_FollowUpRecords_Users_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE
);");

			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FollowUpRecords_DoctorId ON FollowUpRecords (DoctorId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FollowUpRecords_PatientId ON FollowUpRecords (PatientId);");
		}

		private async Task EnsureAdminEntryLogsTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS AdminEntryLogs (
    EntryId INTEGER NOT NULL CONSTRAINT PK_AdminEntryLogs PRIMARY KEY AUTOINCREMENT,
    UserType TEXT NOT NULL,
    Username TEXT NOT NULL,
    FullName TEXT NULL,
    Gender TEXT NULL,
    PhoneNumber TEXT NULL,
    Email TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_AdminEntryLogs_UserType ON AdminEntryLogs (UserType);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_AdminEntryLogs_CreatedAt ON AdminEntryLogs (CreatedAt);");
		}

		private async Task EnsureSystemSettingsTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS SystemSettings (
    Id INTEGER NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY AUTOINCREMENT,
    Key TEXT NOT NULL,
    Value TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_SystemSettings_Key ON SystemSettings (Key);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SystemSettings_UpdatedAt ON SystemSettings (UpdatedAt);");

			// 默认值（仅在不存在时写入）
			await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO SystemSettings (Key, Value, UpdatedAt)
SELECT 'SystemName', '糖尿病足管理系统', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM SystemSettings WHERE Key='SystemName');");
			await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO SystemSettings (Key, Value, UpdatedAt)
SELECT 'MaintenanceAnnouncement', '', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM SystemSettings WHERE Key='MaintenanceAnnouncement');");
			await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO SystemSettings (Key, Value, UpdatedAt)
SELECT 'AllowRegistration', 'true', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM SystemSettings WHERE Key='AllowRegistration');");
			await _context.Database.ExecuteSqlRawAsync(@"
INSERT INTO SystemSettings (Key, Value, UpdatedAt)
SELECT 'MaxUploadMB', '30', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM SystemSettings WHERE Key='MaxUploadMB');");
		}

		private async Task EnsureFollowUpReminderNotificationsTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS FollowUpReminderNotifications (
    Id INTEGER NOT NULL CONSTRAINT PK_FollowUpReminderNotifications PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    FollowUpRecordId INTEGER NOT NULL,
    NextFollowUpDate TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_FollowUpReminderNotifications_Users_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE
);");

			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_FollowUpReminderNotifications_UQ ON FollowUpReminderNotifications (DoctorId, FollowUpRecordId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FollowUpReminderNotifications_DoctorId ON FollowUpReminderNotifications (DoctorId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FollowUpReminderNotifications_PatientId ON FollowUpReminderNotifications (PatientId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FollowUpReminderNotifications_CreatedAt ON FollowUpReminderNotifications (CreatedAt);");

			// 兼容旧数据库：补列
			await AddColumnIfMissingAsync("FollowUpReminderNotifications", "ProcessedAt",
				"ALTER TABLE FollowUpReminderNotifications ADD COLUMN ProcessedAt TEXT NULL;");
		}

		private async Task EnsureDoctorOrderTablesAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS DoctorOrders (
    DoctorOrderId INTEGER NOT NULL CONSTRAINT PK_DoctorOrders PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    Category TEXT NOT NULL,
    Content TEXT NOT NULL,
    StartDate TEXT NOT NULL,
    EndDate TEXT NULL,
    IsActive INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_DoctorOrders_Users_DoctorId FOREIGN KEY (DoctorId) REFERENCES Users (UserId),
    CONSTRAINT FK_DoctorOrders_Users_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DoctorOrders_DoctorId_PatientId ON DoctorOrders (DoctorId, PatientId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DoctorOrders_PatientId ON DoctorOrders (PatientId);");

			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS PatientDailyTasks (
    PatientDailyTaskId INTEGER NOT NULL CONSTRAINT PK_PatientDailyTasks PRIMARY KEY AUTOINCREMENT,
    PatientId INTEGER NOT NULL,
    DoctorOrderId INTEGER NOT NULL,
    TaskDate TEXT NOT NULL,
    IsCompleted INTEGER NOT NULL,
    CompletedAt TEXT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_PatientDailyTasks_Users_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_PatientDailyTasks_DoctorOrders_DoctorOrderId FOREIGN KEY (DoctorOrderId) REFERENCES DoctorOrders (DoctorOrderId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_PatientDailyTasks_UQ ON PatientDailyTasks (PatientId, DoctorOrderId, TaskDate);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_PatientDailyTasks_PatientId_TaskDate ON PatientDailyTasks (PatientId, TaskDate);");
		}

		private async Task EnsureEducationResourcesTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS EducationResources (
    EducationResourceId INTEGER NOT NULL CONSTRAINT PK_EducationResources PRIMARY KEY AUTOINCREMENT,
    UploaderDoctorId INTEGER NOT NULL,
    Title TEXT NOT NULL,
    ResourceType TEXT NOT NULL,
    FileUrl TEXT NOT NULL,
    OriginalFileName TEXT NOT NULL,
    IsActive INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_EducationResources_Users_UploaderDoctorId FOREIGN KEY (UploaderDoctorId) REFERENCES Users (UserId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_EducationResources_UploaderDoctorId ON EducationResources (UploaderDoctorId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_EducationResources_IsActive ON EducationResources (IsActive);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_EducationResources_CreatedAt ON EducationResources (CreatedAt);");
		}

		private async Task EnsureQuestionnaireAssignmentsTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS QuestionnaireAssignments (
    QuestionnaireAssignmentId INTEGER NOT NULL CONSTRAINT PK_QuestionnaireAssignments PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    QuestionnaireJson TEXT NOT NULL,
    AnswerJson TEXT NULL,
    AccessToken TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    SubmittedAt TEXT NULL,
    CONSTRAINT FK_QuestionnaireAssignments_Users_DoctorId FOREIGN KEY (DoctorId) REFERENCES Users (UserId),
    CONSTRAINT FK_QuestionnaireAssignments_Users_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE
);");

			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_QuestionnaireAssignments_DoctorId ON QuestionnaireAssignments (DoctorId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_QuestionnaireAssignments_PatientId ON QuestionnaireAssignments (PatientId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_QuestionnaireAssignments_AccessToken ON QuestionnaireAssignments (AccessToken);");
		}

		private async Task EnsureDoctorHiddenTablesAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS DoctorHiddenHealthItems (
    DoctorHiddenHealthItemId INTEGER NOT NULL CONSTRAINT PK_DoctorHiddenHealthItems PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    ItemKey TEXT NOT NULL,
    HiddenAt TEXT NOT NULL
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_DoctorHiddenHealthItems_UQ ON DoctorHiddenHealthItems (DoctorId, PatientId, ItemKey);");

			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS DoctorHiddenConsultationMessages (
    DoctorHiddenConsultationMessageId INTEGER NOT NULL CONSTRAINT PK_DoctorHiddenConsultationMessages PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    MessageId INTEGER NOT NULL,
    HiddenAt TEXT NOT NULL
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_DoctorHiddenConsultationMessages_UQ ON DoctorHiddenConsultationMessages (DoctorId, PatientId, MessageId);");

			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS DoctorHiddenFollowUpRecords (
    DoctorHiddenFollowUpRecordId INTEGER NOT NULL CONSTRAINT PK_DoctorHiddenFollowUpRecords PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    FollowUpRecordId INTEGER NOT NULL,
    HiddenAt TEXT NOT NULL
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_DoctorHiddenFollowUpRecords_UQ ON DoctorHiddenFollowUpRecords (DoctorId, FollowUpRecordId);");
		}

		private async Task EnsureHighRiskAlertNotificationsTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS HighRiskAlertNotifications (
    NotificationId INTEGER NOT NULL CONSTRAINT PK_HighRiskAlertNotifications PRIMARY KEY AUTOINCREMENT,
    PatientId INTEGER NOT NULL,
    AlertType TEXT NOT NULL,
    Summary TEXT NOT NULL,
    RelatedRecordId INTEGER NULL,
    RelatedTable TEXT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_HighRiskAlertNotifications_Users_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_HighRiskAlertNotifications_PatientId ON HighRiskAlertNotifications (PatientId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_HighRiskAlertNotifications_CreatedAt ON HighRiskAlertNotifications (CreatedAt);");
		}

		private async Task EnsureMedicalExperienceFeedbackTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS MedicalExperienceFeedbacks (
    Id INTEGER NOT NULL CONSTRAINT PK_MedicalExperienceFeedbacks PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    DoctorRating INTEGER NOT NULL,
    OnlineConsultRating INTEGER NOT NULL,
    SystemRating INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_MedicalExperienceFeedbacks_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_MedicalExperienceFeedbacks_UserId ON MedicalExperienceFeedbacks (UserId);");
		}

		private async Task EnsureDoctorCustomGroupTablesAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS DoctorCustomGroups (
    GroupId INTEGER NOT NULL CONSTRAINT PK_DoctorCustomGroups PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    GroupName TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_DoctorCustomGroups_Users_DoctorId FOREIGN KEY (DoctorId) REFERENCES Users (UserId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_DoctorCustomGroups_UQ ON DoctorCustomGroups (DoctorId, GroupName);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DoctorCustomGroups_DoctorId ON DoctorCustomGroups (DoctorId);");

			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS DoctorPatientGroupMaps (
    Id INTEGER NOT NULL CONSTRAINT PK_DoctorPatientGroupMaps PRIMARY KEY AUTOINCREMENT,
    DoctorId INTEGER NOT NULL,
    PatientId INTEGER NOT NULL,
    GroupId INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    CONSTRAINT FK_DoctorPatientGroupMaps_PatientId FOREIGN KEY (PatientId) REFERENCES Users (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_DoctorPatientGroupMaps_GroupId FOREIGN KEY (GroupId) REFERENCES DoctorCustomGroups (GroupId) ON DELETE CASCADE
);");
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_DoctorPatientGroupMaps_UQ ON DoctorPatientGroupMaps (DoctorId, PatientId, GroupId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DoctorPatientGroupMaps_DoctorId ON DoctorPatientGroupMaps (DoctorId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DoctorPatientGroupMaps_PatientId ON DoctorPatientGroupMaps (PatientId);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DoctorPatientGroupMaps_GroupId ON DoctorPatientGroupMaps (GroupId);");
		}

		private async Task EnsureOtherDepartmentDoctorsTableAsync()
		{
			await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS OtherDepartmentDoctors (
    Id INTEGER NOT NULL CONSTRAINT PK_OtherDepartmentDoctors PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Gender TEXT NOT NULL,
    PhoneNumber TEXT NOT NULL,
    Department TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);");

			// 防止重复插入：同一科室同名医生唯一
			await _context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_OtherDepartmentDoctors_UQ ON OtherDepartmentDoctors (Department, Name);");
			await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_OtherDepartmentDoctors_Department ON OtherDepartmentDoctors (Department);");
		}

		private async Task SeedOtherDepartmentDoctorsAsync()
		{
			// 通过唯一索引 + INSERT OR IGNORE，保证可重复执行不产生重复数据
			var now = DateTime.Now.ToString("O");

			await _context.Database.ExecuteSqlRawAsync(@"
INSERT OR IGNORE INTO OtherDepartmentDoctors (Name, Gender, PhoneNumber, Department, CreatedAt)
VALUES
('X医生', '男', '13800000001', '神经外科', @p0),
('Y医生', '男', '13800000002', '血管外科', @p0),
('Z医生', '女', '13800000003', '感染科', @p0);
", now);
		}

		private async Task EnsureUserProfileColumnsAsync()
		{
			await AddUserColumnIfMissingAsync("Age", "ALTER TABLE Users ADD COLUMN Age INTEGER NULL;");
			await AddUserColumnIfMissingAsync("ResidenceStatus", "ALTER TABLE Users ADD COLUMN ResidenceStatus TEXT NULL;");
			await AddUserColumnIfMissingAsync("DiabeticFootType", "ALTER TABLE Users ADD COLUMN DiabeticFootType TEXT NULL;");
			await AddUserColumnIfMissingAsync("DiseaseCourse", "ALTER TABLE Users ADD COLUMN DiseaseCourse TEXT NULL;");
			await AddUserColumnIfMissingAsync("DiagnosisDate", "ALTER TABLE Users ADD COLUMN DiagnosisDate TEXT NULL;");
			await AddUserColumnIfMissingAsync("HadUlcerBeforeVisit", "ALTER TABLE Users ADD COLUMN HadUlcerBeforeVisit TEXT NULL;");
			await AddUserColumnIfMissingAsync("IsPostFootSurgeryPatient", "ALTER TABLE Users ADD COLUMN IsPostFootSurgeryPatient TEXT NULL;");
		}

		private async Task AddUserColumnIfMissingAsync(string columnName, string alterSql)
		{
			await AddColumnIfMissingAsync("Users", columnName, alterSql);
		}

		private async Task AddColumnIfMissingAsync(string tableName, string columnName, string alterSql)
		{
			var connection = _context.Database.GetDbConnection();
			if (connection.State != System.Data.ConnectionState.Open)
			{
				await connection.OpenAsync();
			}

			await using var command = connection.CreateCommand();
			command.CommandText = $"PRAGMA table_info('{tableName}');";
			await using var reader = await command.ExecuteReaderAsync();

			var columnExists = false;
			while (await reader.ReadAsync())
			{
				if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
				{
					columnExists = true;
					break;
				}
			}

			await reader.DisposeAsync();

			if (!columnExists)
			{
				await _context.Database.ExecuteSqlRawAsync(alterSql);
			}
		}
	}
}


