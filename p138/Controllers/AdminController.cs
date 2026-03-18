using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.ViewModels;
using DiabetesPatientApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace DiabetesPatientApp.Controllers
{
	public class AdminController : Controller
	{
	private readonly DiabetesDbContext _context;
	private readonly IAuthService _authService;
	private readonly IWebHostEnvironment _environment;
	private readonly ISystemSettingsService _systemSettings;

	public AdminController(DiabetesDbContext context, IAuthService authService, IWebHostEnvironment environment, ISystemSettingsService systemSettings)
	{
		_context = context;
		_authService = authService;
		_environment = environment;
		_systemSettings = systemSettings;
	}

		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
			if (userId == 0)
			{
				context.Result = RedirectToAction("Login", "Auth");
				return;
			}

			var userType = HttpContext.Session.GetString("UserType");
			if (!string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase))
			{
				context.Result = RedirectToPortalByUserType(userType);
				return;
			}

			base.OnActionExecuting(context);
		}

		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var adminName = HttpContext.Session.GetString("Username") ?? "管理员";

			var recentPatients = await _context.Users
				.Where(u => u.UserType == "Patient")
				.OrderByDescending(u => u.CreatedDate)
				.Take(5)
				.Select(u => new AdminPatientSummary
				{
					UserId = u.UserId,
					FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
					Gender = u.Gender,
					PhoneNumber = u.PhoneNumber,
					CreatedDate = u.CreatedDate,
					IsActive = u.IsActive
				})
				.ToListAsync();

			var recentDoctors = await _context.Users
				.Where(u => u.UserType == "Doctor")
				.OrderByDescending(u => u.CreatedDate)
				.Take(5)
				.Select(u => new AdminDoctorSummary
				{
					UserId = u.UserId,
					FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
					Gender = u.Gender,
					PhoneNumber = u.PhoneNumber,
					ProfessionalTitle = "医生",
					CreatedDate = u.CreatedDate,
					IsActive = u.IsActive
				})
				.ToListAsync();

			var remindersRaw = await _context.Reminders
				.Include(r => r.User)
				.Where(r => r.IsActive)
				.Select(r => new
				{
					r.ReminderId,
					r.MealType,
					r.ReminderTime,
					PatientName = string.IsNullOrWhiteSpace(r.User!.FullName) ? r.User.Username : r.User.FullName
				})
				.ToListAsync();

			// 时间轴：最近7天每日累计数据（高危足预警、换药量、血糖值、伤口上传数、足压记录数、患者数、医生数）
			var today = DateTime.Today;
			var timelineDays = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();
			var patientDates = await _context.Users.Where(u => u.UserType == "Patient").Select(u => u.CreatedDate).ToListAsync();
			var doctorDates = await _context.Users.Where(u => u.UserType == "Doctor").Select(u => u.CreatedDate).ToListAsync();
			var bloodSugarDates = await _context.BloodSugarRecords.Select(r => r.RecordDate).ToListAsync();
			var woundDates = await _context.WoundRecords.Select(r => r.RecordDate).ToListAsync();
			var footDates = await _context.FootPressureRecords.Select(r => r.RecordDate).ToListAsync();
			var highRiskFootDates = await _context.FootPressureRecords
				.Where(r => r.LeftFootStatus == "高风险" || r.LeftFootStatus == "极高风险" || r.RightFootStatus == "高风险" || r.RightFootStatus == "极高风险")
				.Select(r => r.RecordDate)
				.ToListAsync();
			var timelineLabels = timelineDays.Select(d => d.ToString("MM-dd")).ToList();
			var timelineHighRiskFootCounts = timelineDays.Select(d => highRiskFootDates.Count(t => t.Date <= d)).ToList();
			var timelineDressingCounts = timelineDays.Select(d => woundDates.Count(t => t.Date <= d)).ToList();
			var timelineBloodSugarCounts = timelineDays.Select(d => bloodSugarDates.Count(t => t.Date <= d)).ToList();
			var timelineWoundCounts = timelineDays.Select(d => woundDates.Count(t => t.Date <= d)).ToList();
			var timelineFootPressureCounts = timelineDays.Select(d => footDates.Count(t => t.Date <= d)).ToList();
			var timelinePatientCounts = timelineDays.Select(d => patientDates.Count(t => t.Date <= d)).ToList();
			var timelineDoctorCounts = timelineDays.Select(d => doctorDates.Count(t => t.Date <= d)).ToList();

			var model = new AdminDashboardViewModel
			{
				AdminName = adminName,
				PatientCount = await _context.Users.CountAsync(u => u.UserType == "Patient"),
				ActivePatientCount = await _context.Users.CountAsync(u => u.UserType == "Patient" && u.IsActive),
				DoctorCount = await _context.Users.CountAsync(u => u.UserType == "Doctor"),
				BloodSugarRecordCount = await _context.BloodSugarRecords.CountAsync(),
				WoundRecordCount = await _context.WoundRecords.CountAsync(),
				FootPressureRecordCount = await _context.FootPressureRecords.CountAsync(),
				RecentPatients = recentPatients,
				RecentDoctors = recentDoctors,
				UpcomingReminders = remindersRaw
					.OrderBy(r => r.ReminderTime)
					.Take(5)
					.Select(r => new AdminReminderSummary
					{
						ReminderId = r.ReminderId,
						Title = string.IsNullOrWhiteSpace(r.MealType) ? "日常提醒" : r.MealType,
						ReminderTime = DateTime.Today.Add(r.ReminderTime),
						PatientName = r.PatientName ?? "未知患者"
					})
					.ToList(),
				TimelineLabels = timelineLabels,
				TimelineHighRiskFootCounts = timelineHighRiskFootCounts,
				TimelineDressingCounts = timelineDressingCounts,
				TimelineBloodSugarCounts = timelineBloodSugarCounts,
				TimelineWoundCounts = timelineWoundCounts,
				TimelineFootPressureCounts = timelineFootPressureCounts,
				TimelinePatientCounts = timelinePatientCounts,
				TimelineDoctorCounts = timelineDoctorCounts
			};

			return View(model);
		}

		[HttpGet]
		public async Task<IActionResult> DataMaintenance()
		{
			var model = await BuildMaintenanceViewModelAsync(includeUserLists: true);
			model.StatusMessage = TempData["MaintenanceMessage"]?.ToString();
			return View(model);
		}

		[HttpGet]
		public async Task<IActionResult> DataMaintenanceOverview()
		{
			var model = await BuildMaintenanceViewModelAsync(includeUserLists: false);
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddPatient(AdminUserCreateRequest request)
		{
			return await CreateUserAsync(request, "Patient");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddDoctor(AdminUserCreateRequest request)
		{
			return await CreateUserAsync(request, "Doctor");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> BackupUserData()
		{
			var backup = new AdminBackupPackage
			{
				ExportedAt = DateTime.UtcNow,
				Patients = await _context.Users
					.Where(u => u.UserType == "Patient")
					.Select(u => new AdminUserExport
					{
						UserId = u.UserId,
						Username = u.Username ?? string.Empty,
						Email = u.Email ?? string.Empty,
						FullName = u.FullName,
						Gender = u.Gender,
						PhoneNumber = u.PhoneNumber,
						UserType = u.UserType ?? "Patient",
						PasswordHash = u.PasswordHash,
						IsActive = u.IsActive,
						CreatedDate = u.CreatedDate
					})
					.ToListAsync(),
				Doctors = await _context.Users
					.Where(u => u.UserType == "Doctor")
					.Select(u => new AdminUserExport
					{
						UserId = u.UserId,
						Username = u.Username ?? string.Empty,
						Email = u.Email ?? string.Empty,
						FullName = u.FullName,
						Gender = u.Gender,
						PhoneNumber = u.PhoneNumber,
						UserType = u.UserType ?? "Doctor",
						PasswordHash = u.PasswordHash,
						IsActive = u.IsActive,
						CreatedDate = u.CreatedDate
					})
					.ToListAsync()
			};

			var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			var backupDir = Path.Combine(desktop, "DiabetesBackups");
			Directory.CreateDirectory(backupDir);
			var fileName = $"user-backup-{DateTime.Now:yyyyMMddHHmmss}.json";
			var filePath = Path.Combine(backupDir, fileName);
			await System.IO.File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

			TempData["MaintenanceMessage"] = $"备份已保存至 {filePath}";
			return RedirectToAction(nameof(DataMaintenance));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RestoreUserData(IFormFile backupFile)
		{
			if (backupFile == null || backupFile.Length == 0)
			{
				TempData["MaintenanceMessage"] = "请选择有效的备份文件。";
				return RedirectToAction("DataMaintenance");
			}

			try
			{
				using var stream = backupFile.OpenReadStream();
				using var reader = new StreamReader(stream, Encoding.UTF8);
				var json = await reader.ReadToEndAsync();
				var backup = JsonSerializer.Deserialize<AdminBackupPackage>(json);
				if (backup == null)
				{
					TempData["MaintenanceMessage"] = "备份文件格式不正确。";
					return RedirectToAction("DataMaintenance");
				}

				var usersToProcess = new List<AdminUserExport>();
				if (backup.Patients != null) usersToProcess.AddRange(backup.Patients);
				if (backup.Doctors != null) usersToProcess.AddRange(backup.Doctors);

				foreach (var record in usersToProcess)
				{
					if (string.IsNullOrWhiteSpace(record.Username) || string.IsNullOrWhiteSpace(record.Email))
					{
						continue;
					}

					var existing = await _context.Users.FirstOrDefaultAsync(u => u.Username == record.Username);
					if (existing == null)
					{
						existing = new User
						{
							Username = record.Username,
							Email = record.Email,
							PasswordHash = record.PasswordHash ?? _authService.HashPassword("123456"),
							UserType = record.UserType,
							CreatedDate = record.CreatedDate == default ? DateTime.Now : record.CreatedDate,
							IsActive = record.IsActive
						};
						_context.Users.Add(existing);
					}

					existing.FullName = record.FullName;
					existing.Gender = record.Gender;
					existing.PhoneNumber = record.PhoneNumber;
					existing.UserType = record.UserType;
					if (!string.IsNullOrWhiteSpace(record.PasswordHash))
					{
						existing.PasswordHash = record.PasswordHash;
					}
				}

				await _context.SaveChangesAsync();
				TempData["MaintenanceMessage"] = "备份数据已成功恢复。";
			}
			catch (JsonException)
			{
				TempData["MaintenanceMessage"] = "无法解析备份文件，请确认文件内容。";
			}
			catch (System.Exception ex)
			{
				TempData["MaintenanceMessage"] = $"恢复数据失败：{ex.Message}";
			}

			return RedirectToAction("DataMaintenance");
		}

		private async Task<IActionResult> CreateUserAsync(AdminUserCreateRequest request, string userType)
		{
			string targetView;
			string tempKey;
			if (string.Equals(request.ReturnView, nameof(DataMaintenance), StringComparison.OrdinalIgnoreCase))
			{
				targetView = nameof(DataMaintenance);
				tempKey = "MaintenanceMessage";
			}
			else if (string.Equals(request.ReturnView, nameof(Index), StringComparison.OrdinalIgnoreCase))
			{
				targetView = nameof(Index);
				tempKey = "EntryMessage";
			}
			else
			{
				targetView = nameof(Index);
				tempKey = "EntryMessage";
			}

			if (!ModelState.IsValid)
			{
				TempData[tempKey] = "请完整填写新增用户的必填信息。";
				return RedirectToAction(targetView);
			}

			try
			{
				var password = string.IsNullOrWhiteSpace(request.Password) ? "123456" : request.Password;
				var fullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Username : request.FullName;
				var user = await _authService.RegisterAsync(request.Username, request.Email, password, fullName ?? request.Username, userType);
				user.Gender = request.Gender;
				user.PhoneNumber = request.PhoneNumber;
				user.IsActive = true;
				_context.Users.Update(user);
				await _context.SaveChangesAsync();

				try
				{
					_context.AdminEntryLogs.Add(new AdminEntryLog
					{
						UserType = userType,
						Username = user.Username ?? request.Username,
						FullName = user.FullName,
						Gender = user.Gender,
						PhoneNumber = user.PhoneNumber,
						Email = user.Email ?? request.Email,
						CreatedAt = DateTime.UtcNow
					});
					await _context.SaveChangesAsync();
				}
				catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("AdminEntryLogs", StringComparison.OrdinalIgnoreCase) == true)
				{
					foreach (var entry in _context.ChangeTracker.Entries<AdminEntryLog>().ToList())
					{
						entry.State = EntityState.Detached;
					}
				}

				var typeText = userType == "Doctor" ? "医生" : "患者";
				TempData[tempKey] = $"{typeText}资料添加成功。默认密码为 {password} 。";
			}
			catch (System.Exception ex)
			{
				var message = ex.InnerException?.Message ?? ex.Message;
				if (message.Contains("IX_Users_Username", StringComparison.OrdinalIgnoreCase))
				{
					message = "用户名已存在，请更换后重试。";
				}
				else if (message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase))
				{
					message = "邮箱已存在，请更换后重试。";
				}
				else
				{
					message = $"新增失败：{message}";
				}

				TempData[tempKey] = message;
			}

			return RedirectToAction(targetView);
		}

		private async Task<AdminDataMaintenanceViewModel> BuildMaintenanceViewModelAsync(bool includeUserLists)
		{
			var model = new AdminDataMaintenanceViewModel
			{
				InactivePatientCount = await _context.Users.CountAsync(u => u.UserType == "Patient" && !u.IsActive),
				PendingReminderCount = await _context.Reminders.CountAsync(r => r.IsActive),
				OldConsultationMessageCount = await _context.ConsultationMessages.CountAsync(m => !m.IsRead),
				LastPatientLogin = await _context.Users
					.Where(u => u.UserType == "Patient" && u.LastLoginDate != null)
					.OrderByDescending(u => u.LastLoginDate)
					.Select(u => u.LastLoginDate)
					.FirstOrDefaultAsync()
			};

			if (includeUserLists)
			{
				model.Patients = await _context.Users
					.Where(u => u.UserType == "Patient")
					.OrderByDescending(u => u.CreatedDate)
					.Take(20)
					.Select(u => new AdminPatientSummary
					{
						UserId = u.UserId,
						Username = u.Username,
						Email = u.Email,
						FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
						Gender = u.Gender,
						PhoneNumber = u.PhoneNumber,
						CreatedDate = u.CreatedDate,
						IsActive = u.IsActive
					})
					.ToListAsync();

				model.Doctors = await _context.Users
					.Where(u => u.UserType == "Doctor")
					.OrderByDescending(u => u.CreatedDate)
					.Take(20)
					.Select(u => new AdminDoctorSummary
					{
						UserId = u.UserId,
						Username = u.Username,
						Email = u.Email,
						FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
						Gender = u.Gender,
						PhoneNumber = u.PhoneNumber,
						ProfessionalTitle = "主治医师",
						CreatedDate = u.CreatedDate,
						IsActive = u.IsActive
					})
					.ToListAsync();
			}

			return model;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> TogglePatientActive(int userId)
		{
			return await ToggleUserActiveAsync(userId, "Patient");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleDoctorActive(int userId)
		{
			return await ToggleUserActiveAsync(userId, "Doctor");
		}

		private async Task<IActionResult> ToggleUserActiveAsync(int userId, string expectedType)
		{
			try
			{
				var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.UserType == expectedType);
				if (user == null)
				{
					TempData["MaintenanceMessage"] = $"{(expectedType == "Doctor" ? "医生" : "患者")}不存在或类型不匹配。";
					return RedirectToAction(nameof(DataMaintenance));
				}

				user.IsActive = !user.IsActive;
				_context.Users.Update(user);
				await _context.SaveChangesAsync();
				TempData["MaintenanceMessage"] = $"{(expectedType == "Doctor" ? "医生" : "患者")}已{(user.IsActive ? "启用" : "停用")}。";
			}
			catch (Exception ex)
			{
				TempData["MaintenanceMessage"] = $"操作失败：{ex.Message}";
			}

			return RedirectToAction(nameof(DataMaintenance));
		}

		[HttpGet]
		public async Task<IActionResult> Monitor()
		{
			// 系统监控：快速健康检查 + 最近新增账号日志
			var now = DateTime.Now;
			var dbOk = true;
			string? dbError = null;
			try
			{
				await _context.Users.AsNoTracking().Select(x => x.UserId).Take(1).ToListAsync();
			}
			catch (Exception ex)
			{
				dbOk = false;
				dbError = ex.Message;
			}

			var recentEntries = new List<AdminEntryLog>();
			try
			{
				recentEntries = await _context.AdminEntryLogs
					.AsNoTracking()
					.OrderByDescending(x => x.EntryId)
					.Take(50)
					.ToListAsync();
			}
			catch
			{
				// 忽略：兼容旧库或未生成表的情况
			}

			ViewBag.Now = now;
			ViewBag.DbOk = dbOk;
			ViewBag.DbError = dbError;
			ViewBag.PatientCount = await _context.Users.CountAsync(u => u.UserType == "Patient");
			ViewBag.DoctorCount = await _context.Users.CountAsync(u => u.UserType == "Doctor");
			ViewBag.BloodSugarCount = await _context.BloodSugarRecords.CountAsync();
			ViewBag.WoundCount = await _context.WoundRecords.CountAsync();
			ViewBag.FootPressureCount = await _context.FootPressureRecords.CountAsync();
			ViewBag.ConsultationCount = await _context.ConsultationMessages.CountAsync();

			// 基础设置（用于全站展示/开关）
			var settings = await _systemSettings.GetAllAsync();
			ViewBag.SystemName = settings.TryGetValue(SystemSettingKeys.SystemName, out var n) ? n : "糖尿病足管理系统";
			ViewBag.MaintenanceAnnouncement = settings.TryGetValue(SystemSettingKeys.MaintenanceAnnouncement, out var a) ? a : "";
			ViewBag.AllowRegistration = settings.TryGetValue(SystemSettingKeys.AllowRegistration, out var r) ? r : "true";
			ViewBag.MaxUploadMB = settings.TryGetValue(SystemSettingKeys.MaxUploadMB, out var m) ? m : "30";

			return View(recentEntries);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SaveBasicSettings(string systemName, string maintenanceAnnouncement, string allowRegistration, string maxUploadMB)
		{
			var dict = new Dictionary<string, string>
			{
				[SystemSettingKeys.SystemName] = (systemName ?? "").Trim(),
				[SystemSettingKeys.MaintenanceAnnouncement] = (maintenanceAnnouncement ?? "").Trim(),
				[SystemSettingKeys.AllowRegistration] = (allowRegistration ?? "true").Trim(),
				[SystemSettingKeys.MaxUploadMB] = (maxUploadMB ?? "30").Trim()
			};

			await _systemSettings.UpsertManyAsync(dict);
			TempData["MaintenanceMessage"] = "基础设置已保存（全站立即生效）。";
			return RedirectToAction(nameof(Monitor));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> BackfillEntryLogs(int take = 100)
		{
			// 兼容：如果之前未建表/写入失败，可对最近账号补记日志
			take = take <= 0 ? 100 : Math.Min(take, 500);
			var created = 0;
			try
			{
				var users = await _context.Users
					.AsNoTracking()
					.Where(u => u.UserType == "Patient" || u.UserType == "Doctor")
					.OrderByDescending(u => u.CreatedDate)
					.Take(take)
					.Select(u => new
					{
						u.UserType,
						u.Username,
						u.FullName,
						u.Gender,
						u.PhoneNumber,
						u.Email,
						u.CreatedDate
					})
					.ToListAsync();

				foreach (var u in users)
				{
					var email = u.Email ?? "";
					if (string.IsNullOrWhiteSpace(email)) continue;
					var exists = await _context.AdminEntryLogs.AsNoTracking()
						.AnyAsync(x => x.UserType == u.UserType && x.Email == email);
					if (exists) continue;

					_context.AdminEntryLogs.Add(new AdminEntryLog
					{
						UserType = u.UserType ?? "Patient",
						Username = u.Username ?? email,
						FullName = u.FullName,
						Gender = u.Gender,
						PhoneNumber = u.PhoneNumber,
						Email = email,
						CreatedAt = DateTime.UtcNow
					});
					created++;
				}

				if (created > 0)
				{
					await _context.SaveChangesAsync();
				}
			}
			catch (Exception ex)
			{
				TempData["MaintenanceMessage"] = $"补记日志失败：{ex.Message}";
				return RedirectToAction(nameof(Monitor));
			}

			TempData["MaintenanceMessage"] = created > 0 ? $"已补记 {created} 条新增账号日志。" : "无需补记：日志已存在或无可补记数据。";
			return RedirectToAction(nameof(Monitor));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdatePatient(AdminUserUpdateRequest request)
		{
			return await UpdateUserAsync(request, "Patient");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateDoctor(AdminUserUpdateRequest request)
		{
			return await UpdateUserAsync(request, "Doctor");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeletePatient(int userId)
		{
			return await DeleteUserAsync(userId, "Patient");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteDoctor(int userId)
		{
			return await DeleteUserAsync(userId, "Doctor");
		}

		private async Task<IActionResult> UpdateUserAsync(AdminUserUpdateRequest request, string expectedType)
		{
			try
			{
				var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId && u.UserType == expectedType);
				if (user == null)
				{
					TempData["MaintenanceMessage"] = $"{(expectedType == "Doctor" ? "医生" : "患者")}不存在或类型不匹配。";
					return RedirectToAction(nameof(DataMaintenance));
				}

				user.FullName = request.FullName;
				user.PhoneNumber = request.PhoneNumber;
				if (!string.IsNullOrWhiteSpace(request.Password))
				{
					user.PasswordHash = _authService.HashPassword(request.Password);
				}

				_context.Users.Update(user);
				await _context.SaveChangesAsync();
				TempData["MaintenanceMessage"] = $"{(expectedType == "Doctor" ? "医生" : "患者")}信息已更新。";
			}
			catch (System.Exception ex)
			{
				TempData["MaintenanceMessage"] = $"更新失败：{ex.Message}";
			}

			return RedirectToAction(nameof(DataMaintenance));
		}

		private async Task<IActionResult> DeleteUserAsync(int userId, string expectedType)
		{
			try
			{
				var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.UserType == expectedType);
				if (user == null)
				{
					TempData["MaintenanceMessage"] = $"{(expectedType == "Doctor" ? "医生" : "患者")}不存在或类型不匹配。";
					return RedirectToAction(nameof(DataMaintenance));
				}

				_context.Users.Remove(user);
				await _context.SaveChangesAsync();
				TempData["MaintenanceMessage"] = $"{(expectedType == "Doctor" ? "医生" : "患者")}已删除。";
			}
			catch (System.Exception ex)
			{
				TempData["MaintenanceMessage"] = $"删除失败：{ex.Message}";
			}

			return RedirectToAction(nameof(DataMaintenance));
		}

		[HttpGet]
		public IActionResult DataEntry()
		{
			return RedirectToAction(nameof(Index));
		}

		[HttpGet]
		public async Task<IActionResult> Analytics()
		{
			var bloodSugarQuery = _context.BloodSugarRecords.AsQueryable();
			double? averageBloodSugar = null;
			if (await bloodSugarQuery.AnyAsync())
			{
				averageBloodSugar = await bloodSugarQuery.AverageAsync(r => (double)(r.BloodSugarValue / 18m));
			}

			var totalBloodSugar = await bloodSugarQuery.CountAsync();
			var highRiskBloodSugar = await bloodSugarQuery.CountAsync(r => r.Status == "High");
			var totalFootPressure = await _context.FootPressureRecords.CountAsync();
			var highPressureCases = await _context.FootPressureRecords.CountAsync(r => r.LeftFootStatus == "高风险" || r.RightFootStatus == "高风险" || r.LeftFootStatus == "极高风险" || r.RightFootStatus == "极高风险");
			var totalWound = await _context.WoundRecords.CountAsync();
			var openWoundCases = await _context.WoundRecords.CountAsync(r => r.HasInfection || r.HasDischarge || r.HasFever || r.HasOdor);
			var consultationCount = await _context.ConsultationMessages.CountAsync();
			var activeReminderCount = await _context.Reminders.CountAsync(r => r.IsActive);
			var followUpCount = await _context.FollowUpRecords.CountAsync();
			var questionnaireCount = await _context.QuestionnaireAssignments.CountAsync();
			var postCount = await _context.Posts.CountAsync();
			var commentCount = await _context.Comments.CountAsync();
			var patientCount = await _context.Users.CountAsync(u => u.UserType == "Patient");
			var doctorCount = await _context.Users.CountAsync(u => u.UserType == "Doctor");

			var model = new AdminAnalyticsViewModel
			{
				PatientCount = patientCount,
				DoctorCount = doctorCount,
				TotalBloodSugarRecords = totalBloodSugar,
				AverageBloodSugar = averageBloodSugar,
				HighRiskBloodSugarCount = highRiskBloodSugar,
				TotalFootPressureRecords = totalFootPressure,
				HighPressureCases = highPressureCases,
				TotalWoundRecords = totalWound,
				OpenWoundCases = openWoundCases,
				ConsultationMessageCount = consultationCount,
				ActiveReminderCount = activeReminderCount,
				FollowUpRecordCount = followUpCount,
				QuestionnaireCount = questionnaireCount,
				PostCount = postCount,
				CommentCount = commentCount,
				PatientChartLabels = new List<string> { "血糖记录", "伤口上传", "足压记录", "启用提醒", "咨询消息", "社区发帖" },
				PatientChartData = new List<int> { totalBloodSugar, totalWound, totalFootPressure, activeReminderCount, consultationCount, postCount },
				DoctorChartLabels = new List<string> { "咨询消息", "回访登记", "健康问卷", "伤口记录查阅" },
				DoctorChartData = new List<int> { consultationCount, followUpCount, questionnaireCount, totalWound }
			};

			model.AnalysisConclusions = BuildAnalyticsConclusions(model);
			return View(model);
		}

		private static List<AnalyticsConclusionItem> BuildAnalyticsConclusions(AdminAnalyticsViewModel m)
		{
			var list = new List<AnalyticsConclusionItem>();
			if (m.TotalBloodSugarRecords > 0)
			{
				var highRate = (double)m.HighRiskBloodSugarCount / m.TotalBloodSugarRecords * 100;
				if (highRate >= 30)
					list.Add(new AnalyticsConclusionItem { Level = "Warning", Title = "血糖高危占比偏高", Content = $"当前高危血糖记录占 {highRate:F0}%（{m.HighRiskBloodSugarCount}/{m.TotalBloodSugarRecords}），建议加强患者血糖监测与用药随访。" });
				else if (highRate >= 15)
					list.Add(new AnalyticsConclusionItem { Level = "Info", Title = "血糖监测提示", Content = $"高危血糖记录占 {highRate:F0}%，可引导患者规律测血糖并关注餐后与空腹达标情况。" });
				if (m.AverageBloodSugar.HasValue && m.AverageBloodSugar.Value > 7.0)
					list.Add(new AnalyticsConclusionItem { Level = "Warning", Title = "整体血糖均值偏高", Content = $"当前血糖均值为 {m.AverageBloodSugar.Value:F1} mmol/L，建议强化饮食与用药指导，鼓励患者端持续记录以便追踪。" });
			}
			if (m.TotalFootPressureRecords > 0)
			{
				var highRate = (double)m.HighPressureCases / m.TotalFootPressureRecords * 100;
				if (highRate >= 25)
					list.Add(new AnalyticsConclusionItem { Level = "Warning", Title = "高危足占比较高", Content = $"高危/极高风险足压记录占 {highRate:F0}%（{m.HighPressureCases}/{m.TotalFootPressureRecords}），建议重点随访足部状况并加强患者教育。" });
			}
			if (m.TotalWoundRecords > 0 && m.OpenWoundCases > 0)
				list.Add(new AnalyticsConclusionItem { Level = "Warning", Title = "存在伤口异常记录", Content = $"共 {m.OpenWoundCases} 条记录存在感染/渗出/发热/异味等异常，建议医生端关注相关患者并督促换药与复查。" });
			if (m.PatientCount > 0 && m.ConsultationMessageCount == 0)
				list.Add(new AnalyticsConclusionItem { Level = "Info", Title = "咨询参与度可提升", Content = "当前尚无咨询消息，可引导患者使用患者端「在线咨询」与医生沟通。" });
			else if (m.PatientCount >= 3 && m.ConsultationMessageCount < m.PatientCount * 2)
				list.Add(new AnalyticsConclusionItem { Level = "Info", Title = "咨询与互动", Content = "可继续引导患者通过在线咨询获取医嘱，提升医患互动频率。" });
			if (m.DoctorCount > 0 && m.FollowUpRecordCount == 0)
				list.Add(new AnalyticsConclusionItem { Level = "Info", Title = "回访登记", Content = "建议医生端使用「回访登记」记录随访，便于统计与提醒复诊。" });
			if (m.PatientCount > 0 && m.ActiveReminderCount == 0)
				list.Add(new AnalyticsConclusionItem { Level = "Info", Title = "提醒设置", Content = "暂无启用提醒，可引导患者在患者端「提醒设置」中配置用药/测血糖等提醒。" });
			if (list.Count == 0)
				list.Add(new AnalyticsConclusionItem { Level = "Success", Title = "数据概况", Content = "当前统计未见明显风险提示；持续积累数据后，本分析将根据血糖、足压、伤口等指标给出更多建议。" });
			return list;
		}

		[HttpGet]
		public IActionResult DocumentManagement()
		{
			var model = new AdminDocumentManagementViewModel
			{
				Files = GetDocumentFiles(),
				StatusMessage = TempData["DocumentMessage"]?.ToString()
			};

			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UploadDocument(IFormFile[] uploadFiles)
		{
			if (uploadFiles == null || uploadFiles.Length == 0)
			{
				TempData["DocumentMessage"] = "请选择要上传的文件。";
				return RedirectToAction(nameof(DocumentManagement));
			}

			var maxMb = await _systemSettings.GetIntAsync(SystemSettingKeys.MaxUploadMB, 30);
			if (maxMb > 0)
			{
				foreach (var f in uploadFiles)
				{
					if (f != null && f.Length > (long)maxMb * 1024L * 1024L)
					{
						TempData["DocumentMessage"] = $"上传失败：文件 {Path.GetFileName(f.FileName)} 过大，单个文件请 ≤ {maxMb}MB。";
						return RedirectToAction(nameof(DocumentManagement));
					}
				}
			}

			var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
			var textExtensions = new[] { ".txt", ".md", ".markdown", ".html", ".htm" };

			var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
			var imageDir = Path.Combine(webRoot, "uploads", "carousel");
			var textDir = Path.Combine(webRoot, "uploads", "articles");
			Directory.CreateDirectory(imageDir);
			Directory.CreateDirectory(textDir);

			var successList = new List<string>();
			var skippedList = new List<string>();

			foreach (var file in uploadFiles)
			{
				if (file == null || file.Length == 0) continue;

				var sanitizedName = Path.GetFileName(file.FileName);
				var extension = Path.GetExtension(sanitizedName).ToLowerInvariant();
				string? targetDir = null;
				string categoryMessage;

				if (imageExtensions.Contains(extension))
				{
					targetDir = imageDir;
					categoryMessage = "已加入患者首页轮播。";
				}
				else if (textExtensions.Contains(extension))
				{
					targetDir = textDir;
					categoryMessage = "已同步至患者首页宣教内容。";
				}
				else
				{
					skippedList.Add($"{sanitizedName}（类型不支持）");
					continue;
				}

				try
				{
					var uniqueName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{sanitizedName}";
					var filePath = Path.Combine(targetDir, uniqueName);
					using (var stream = System.IO.File.Create(filePath))
					{
						await file.CopyToAsync(stream);
					}
					successList.Add($"{sanitizedName}：{categoryMessage}");
				}
				catch (Exception ex)
				{
					skippedList.Add($"{sanitizedName}（上传失败：{ex.Message}）");
				}
			}

			var messageBuilder = new List<string>();
			if (successList.Any())
			{
				messageBuilder.Add("成功上传：" + string.Join("；", successList));
			}
			if (skippedList.Any())
			{
				messageBuilder.Add("跳过：" + string.Join("；", skippedList));
			}
			if (!messageBuilder.Any())
			{
				messageBuilder.Add("未成功上传任何文件。");
			}
			TempData["DocumentMessage"] = string.Join("  ", messageBuilder);

			return RedirectToAction(nameof(DocumentManagement));
		}

		private IEnumerable<AdminDocumentFile> GetDocumentFiles()
		{
			var list = new List<AdminDocumentFile>();
			var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

			void Collect(string subFolder, string category)
			{
				var dir = Path.Combine(webRoot, "uploads", subFolder);
				if (!Directory.Exists(dir))
				{
					return;
				}

				foreach (var file in Directory.GetFiles(dir))
				{
					var info = new FileInfo(file);
					list.Add(new AdminDocumentFile
					{
						FileName = info.Name,
						DisplayName = info.Name,
						Url = "/" + Path.GetRelativePath(webRoot, file).Replace("\\", "/"),
						RelativePath = Path.GetRelativePath(webRoot, file).Replace("\\", "/"),
						SizeBytes = info.Length,
						UploadedAt = info.LastWriteTime,
						Category = category
					});
				}
			}

			Collect("carousel", "患者首页轮播图");
			Collect("articles", "患者首页宣教内容");

			return list
				.OrderByDescending(f => f.UploadedAt)
				.ToList();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult DeleteDocument(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
			{
				TempData["DocumentMessage"] = "未找到需要撤回的文件。";
				return RedirectToAction(nameof(DocumentManagement));
			}

			try
			{
				var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
				var fullPath = Path.Combine(webRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

				if (!fullPath.StartsWith(Path.Combine(webRoot, "uploads"), StringComparison.OrdinalIgnoreCase))
				{
					TempData["DocumentMessage"] = "无效的文件路径。";
					return RedirectToAction(nameof(DocumentManagement));
				}

				if (System.IO.File.Exists(fullPath))
				{
					System.IO.File.Delete(fullPath);
					TempData["DocumentMessage"] = "文件已成功撤回。";
				}
				else
				{
					TempData["DocumentMessage"] = "文件不存在或已被移除。";
				}
			}
			catch (Exception ex)
			{
				TempData["DocumentMessage"] = $"撤回失败：{ex.Message}";
			}

			return RedirectToAction(nameof(DocumentManagement));
		}

		private IActionResult RedirectToPortalByUserType(string? userType)
		{
			if (string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
			{
				return RedirectToAction("Index", "Dashboard");
			}

			if (string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
			{
				return RedirectToAction("Index", "Doctor");
			}

			HttpContext.Session.Clear();
			return RedirectToAction("Login", "Auth");
		}
	}
}

