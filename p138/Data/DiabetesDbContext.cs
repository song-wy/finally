using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Data
{
    public class DiabetesDbContext : DbContext
    {
        public DiabetesDbContext(DbContextOptions<DiabetesDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<BloodSugarRecord> BloodSugarRecords { get; set; }
        public DbSet<WoundRecord> WoundRecords { get; set; }
        public DbSet<ConsultationMessage> ConsultationMessages { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<FootPressureRecord> FootPressureRecords { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<AdminEntryLog> AdminEntryLogs { get; set; }
        public DbSet<DoctorProfile> DoctorProfiles { get; set; }
        public DbSet<FollowUpRecord> FollowUpRecords { get; set; }
        public DbSet<DoctorHiddenHealthItem> DoctorHiddenHealthItems { get; set; }
        public DbSet<DoctorHiddenConsultationMessage> DoctorHiddenConsultationMessages { get; set; }
        public DbSet<DoctorHiddenFollowUpRecord> DoctorHiddenFollowUpRecords { get; set; }
        public DbSet<QuestionnaireAssignment> QuestionnaireAssignments { get; set; }
        public DbSet<HighRiskAlertNotification> HighRiskAlertNotifications { get; set; }
        public DbSet<MedicalExperienceFeedback> MedicalExperienceFeedbacks { get; set; }
        public DbSet<DoctorCustomGroup> DoctorCustomGroups { get; set; }
        public DbSet<DoctorPatientGroupMap> DoctorPatientGroupMaps { get; set; }
        public DbSet<OtherDepartmentDoctor> OtherDepartmentDoctors { get; set; }
        public DbSet<FollowUpReminderNotification> FollowUpReminderNotifications { get; set; }
        public DbSet<DoctorOrder> DoctorOrders { get; set; }
        public DbSet<PatientDailyTask> PatientDailyTasks { get; set; }
        public DbSet<EducationResource> EducationResources { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>()
                .HasKey(u => u.UserId);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
            modelBuilder.Entity<User>()
                .Property(u => u.ResidenceStatus)
                .HasMaxLength(100);
            modelBuilder.Entity<User>()
                .Property(u => u.DiabeticFootType)
                .HasMaxLength(100);
            modelBuilder.Entity<User>()
                .Property(u => u.DiseaseCourse)
                .HasMaxLength(100);

            modelBuilder.Entity<DoctorProfile>()
                .HasKey(d => d.DoctorProfileId);
            modelBuilder.Entity<DoctorProfile>()
                .HasIndex(d => d.UserId)
                .IsUnique();
            modelBuilder.Entity<DoctorProfile>()
                .Property(d => d.Department)
                .HasMaxLength(100);
            modelBuilder.Entity<DoctorProfile>()
                .Property(d => d.ProfessionalTitle)
                .HasMaxLength(100);
            modelBuilder.Entity<DoctorProfile>()
                .Property(d => d.HospitalName)
                .HasMaxLength(200);
            modelBuilder.Entity<DoctorProfile>()
                .Property(d => d.Specialty)
                .HasMaxLength(300);
            modelBuilder.Entity<DoctorProfile>()
                .Property(d => d.ConsultationHours)
                .HasMaxLength(200);
            modelBuilder.Entity<DoctorProfile>()
                .Property(d => d.ClinicAddress)
                .HasMaxLength(300);
            modelBuilder.Entity<DoctorProfile>()
                .HasOne(d => d.User)
                .WithOne(u => u.DoctorProfile)
                .HasForeignKey<DoctorProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // BloodSugarRecord configuration
            modelBuilder.Entity<BloodSugarRecord>()
                .HasKey(b => b.RecordId);
            modelBuilder.Entity<BloodSugarRecord>()
                .HasOne(b => b.User)
                .WithMany(u => u.BloodSugarRecords)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<BloodSugarRecord>()
                .Property(b => b.BloodSugarValue)
                .HasPrecision(10, 2);

            // WoundRecord configuration
            modelBuilder.Entity<WoundRecord>()
                .HasKey(w => w.WoundId);
            modelBuilder.Entity<WoundRecord>()
                .HasOne(w => w.User)
                .WithMany(u => u.WoundRecords)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<WoundRecord>()
                .Property(w => w.SurfaceTemperature)
                .HasPrecision(5, 2);

            // ConsultationMessage configuration
            modelBuilder.Entity<ConsultationMessage>()
                .HasKey(c => c.MessageId);
            modelBuilder.Entity<ConsultationMessage>()
                .HasOne(c => c.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(c => c.SenderId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ConsultationMessage>()
                .HasOne(c => c.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(c => c.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            // Reminder configuration
            modelBuilder.Entity<Reminder>()
                .HasKey(r => r.ReminderId);
            modelBuilder.Entity<Reminder>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // FootPressureRecord configuration
            modelBuilder.Entity<FootPressureRecord>()
                .HasKey(f => f.FootPressureId);
            modelBuilder.Entity<FootPressureRecord>()
                .HasOne(f => f.User)
                .WithMany(u => u.FootPressureRecords)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<FootPressureRecord>()
                .Property(f => f.LeftFootPressure)
                .HasPrecision(10, 2);
            modelBuilder.Entity<FootPressureRecord>()
                .Property(f => f.RightFootPressure)
                .HasPrecision(10, 2);

            // Post configuration
            modelBuilder.Entity<Post>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Comment configuration
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AdminEntryLog>()
                .HasKey(a => a.EntryId);
            modelBuilder.Entity<AdminEntryLog>()
                .Property(a => a.UserType)
                .HasMaxLength(20);
            modelBuilder.Entity<AdminEntryLog>()
                .Property(a => a.Username)
                .HasMaxLength(100);
            modelBuilder.Entity<AdminEntryLog>()
                .Property(a => a.Email)
                .HasMaxLength(100);

            modelBuilder.Entity<FollowUpRecord>()
                .HasKey(f => f.FollowUpRecordId);
            modelBuilder.Entity<FollowUpRecord>()
                .Property(f => f.FollowUpMethod)
                .HasMaxLength(50);
            modelBuilder.Entity<FollowUpRecord>()
                .HasOne(f => f.Doctor)
                .WithMany()
                .HasForeignKey(f => f.DoctorId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FollowUpRecord>()
                .HasOne(f => f.Patient)
                .WithMany()
                .HasForeignKey(f => f.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<FollowUpRecord>()
                .HasIndex(f => f.DoctorId);
            modelBuilder.Entity<FollowUpRecord>()
                .HasIndex(f => f.PatientId);

            modelBuilder.Entity<FollowUpReminderNotification>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<FollowUpReminderNotification>()
                .HasIndex(x => new { x.DoctorId, x.FollowUpRecordId })
                .IsUnique();
            modelBuilder.Entity<FollowUpReminderNotification>()
                .HasIndex(x => x.DoctorId);
            modelBuilder.Entity<FollowUpReminderNotification>()
                .HasIndex(x => x.PatientId);
            modelBuilder.Entity<FollowUpReminderNotification>()
                .HasOne(x => x.Patient)
                .WithMany()
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DoctorOrder>()
                .HasKey(x => x.DoctorOrderId);
            modelBuilder.Entity<DoctorOrder>()
                .HasIndex(x => new { x.DoctorId, x.PatientId });
            modelBuilder.Entity<DoctorOrder>()
                .HasIndex(x => x.PatientId);

            modelBuilder.Entity<PatientDailyTask>()
                .HasKey(x => x.PatientDailyTaskId);
            modelBuilder.Entity<PatientDailyTask>()
                .HasIndex(x => new { x.PatientId, x.TaskDate });
            modelBuilder.Entity<PatientDailyTask>()
                .HasIndex(x => new { x.PatientId, x.DoctorOrderId, x.TaskDate })
                .IsUnique();

            modelBuilder.Entity<EducationResource>()
                .HasKey(x => x.EducationResourceId);
            modelBuilder.Entity<EducationResource>()
                .HasIndex(x => x.UploaderDoctorId);
            modelBuilder.Entity<EducationResource>()
                .HasIndex(x => x.IsActive);
            modelBuilder.Entity<EducationResource>()
                .HasIndex(x => x.CreatedAt);

            modelBuilder.Entity<SystemSetting>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<SystemSetting>()
                .HasIndex(x => x.Key)
                .IsUnique();

            modelBuilder.Entity<DoctorHiddenHealthItem>()
                .HasKey(x => x.DoctorHiddenHealthItemId);
            modelBuilder.Entity<DoctorHiddenHealthItem>()
                .Property(x => x.ItemKey)
                .HasMaxLength(100);
            modelBuilder.Entity<DoctorHiddenHealthItem>()
                .HasIndex(x => new { x.DoctorId, x.PatientId, x.ItemKey })
                .IsUnique();

            modelBuilder.Entity<DoctorHiddenConsultationMessage>()
                .HasKey(x => x.DoctorHiddenConsultationMessageId);
            modelBuilder.Entity<DoctorHiddenConsultationMessage>()
                .HasIndex(x => new { x.DoctorId, x.PatientId, x.MessageId })
                .IsUnique();

            modelBuilder.Entity<DoctorHiddenFollowUpRecord>()
                .HasKey(x => x.DoctorHiddenFollowUpRecordId);
            modelBuilder.Entity<DoctorHiddenFollowUpRecord>()
                .HasIndex(x => new { x.DoctorId, x.FollowUpRecordId })
                .IsUnique();

            modelBuilder.Entity<QuestionnaireAssignment>()
                .HasKey(q => q.QuestionnaireAssignmentId);
            modelBuilder.Entity<QuestionnaireAssignment>()
                .Property(q => q.AccessToken)
                .HasMaxLength(80);
            modelBuilder.Entity<QuestionnaireAssignment>()
                .HasIndex(q => new { q.DoctorId, q.PatientId });
            modelBuilder.Entity<QuestionnaireAssignment>()
                .HasIndex(q => q.AccessToken);

            modelBuilder.Entity<HighRiskAlertNotification>()
                .HasKey(n => n.NotificationId);
            modelBuilder.Entity<HighRiskAlertNotification>()
                .Property(n => n.AlertType)
                .HasMaxLength(32);
            modelBuilder.Entity<HighRiskAlertNotification>()
                .Property(n => n.Summary)
                .HasMaxLength(500);
            modelBuilder.Entity<HighRiskAlertNotification>()
                .Property(n => n.RelatedTable)
                .HasMaxLength(32);
            modelBuilder.Entity<HighRiskAlertNotification>()
                .HasOne(n => n.Patient)
                .WithMany()
                .HasForeignKey(n => n.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<HighRiskAlertNotification>()
                .HasIndex(n => n.PatientId);
            modelBuilder.Entity<HighRiskAlertNotification>()
                .HasIndex(n => n.CreatedAt);

            modelBuilder.Entity<MedicalExperienceFeedback>()
                .HasKey(f => f.Id);
            modelBuilder.Entity<MedicalExperienceFeedback>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MedicalExperienceFeedback>()
                .HasIndex(f => f.UserId);

            modelBuilder.Entity<DoctorCustomGroup>()
                .HasKey(g => g.GroupId);
            modelBuilder.Entity<DoctorCustomGroup>()
                .Property(g => g.GroupName)
                .HasMaxLength(50);
            modelBuilder.Entity<DoctorCustomGroup>()
                .HasIndex(g => new { g.DoctorId, g.GroupName })
                .IsUnique();
            modelBuilder.Entity<DoctorCustomGroup>()
                .HasIndex(g => g.DoctorId);

            modelBuilder.Entity<DoctorPatientGroupMap>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<DoctorPatientGroupMap>()
                .HasOne(m => m.Group)
                .WithMany()
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DoctorPatientGroupMap>()
                .HasOne(m => m.Patient)
                .WithMany()
                .HasForeignKey(m => m.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DoctorPatientGroupMap>()
                .HasIndex(m => new { m.DoctorId, m.PatientId, m.GroupId })
                .IsUnique();
        }
    }
}

