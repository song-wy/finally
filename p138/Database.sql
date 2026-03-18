CREATE DATABASE DiabetesPatientDB;
GO

USE DiabetesPatientDB;
GO

CREATE TABLE Users (
    UserId INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    FullName NVARCHAR(100),
    PhoneNumber NVARCHAR(20),
    DateOfBirth DATE,
    Gender NVARCHAR(10),
    UserType NVARCHAR(20) NOT NULL,
    CreatedDate DATETIME DEFAULT GETDATE(),
    LastLoginDate DATETIME,
    IsActive BIT DEFAULT 1
);
GO

CREATE TABLE AdminEntryLogs (
    EntryId INT PRIMARY KEY IDENTITY(1,1),
    UserType NVARCHAR(20) NOT NULL,
    Username NVARCHAR(100) NOT NULL,
    FullName NVARCHAR(100),
    Gender NVARCHAR(10),
    PhoneNumber NVARCHAR(20),
    Email NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

CREATE TABLE DoctorProfiles (
    DoctorProfileId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL UNIQUE,
    Department NVARCHAR(100) NOT NULL DEFAULT '',
    ProfessionalTitle NVARCHAR(100) NOT NULL DEFAULT '',
    HospitalName NVARCHAR(200) NOT NULL DEFAULT '',
    Specialty NVARCHAR(300) NOT NULL DEFAULT '',
    Introduction NVARCHAR(MAX) NOT NULL DEFAULT '',
    ConsultationHours NVARCHAR(200) NOT NULL DEFAULT '',
    ClinicAddress NVARCHAR(300) NOT NULL DEFAULT '',
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE BloodSugarRecords (
    RecordId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    RecordDate DATE NOT NULL,
    RecordTime TIME NOT NULL,
    MealType NVARCHAR(50) NOT NULL,
    BloodSugarValue DECIMAL(5,2) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    Notes NVARCHAR(MAX),
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE Reminders (
    ReminderId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    MealType NVARCHAR(50) NOT NULL,
    ReminderTime TIME NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE WoundRecords (
    WoundId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    RecordDate DATE NOT NULL,
    RecordTime TIME NOT NULL,
    SurfaceTemperature DECIMAL(5,2),
    WoundStatus NVARCHAR(MAX),
    HasInfection BIT DEFAULT 0,
    HasFever BIT DEFAULT 0,
    HasOdor BIT DEFAULT 0,
    HasDischarge BIT DEFAULT 0,
    PhotoPath NVARCHAR(MAX),
    Notes NVARCHAR(MAX),
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE FootPressureRecords (
    FootPressureId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    RecordDate DATE NOT NULL,
    RecordTime TIME NOT NULL,
    LeftFootPressure DECIMAL(5,2),
    LeftFootStatus NVARCHAR(50),
    RightFootPressure DECIMAL(5,2),
    RightFootStatus NVARCHAR(50),
    OverallAssessment NVARCHAR(MAX),
    Notes NVARCHAR(MAX),
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE ConsultationMessages (
    MessageId INT PRIMARY KEY IDENTITY(1,1),
    SenderId INT NOT NULL,
    ReceiverId INT,
    MessageType NVARCHAR(20) NOT NULL,
    MessageContent NVARCHAR(MAX),
    VoiceFilePath NVARCHAR(MAX),
    IsRead BIT DEFAULT 0,
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (SenderId) REFERENCES Users(UserId),
    FOREIGN KEY (ReceiverId) REFERENCES Users(UserId)
);
GO

CREATE TABLE PatientDoctorRelationship (
    RelationshipId INT PRIMARY KEY IDENTITY(1,1),
    PatientId INT NOT NULL,
    DoctorId INT NOT NULL,
    AssignedDate DATETIME DEFAULT GETDATE(),
    IsActive BIT DEFAULT 1,
    FOREIGN KEY (PatientId) REFERENCES Users(UserId),
    FOREIGN KEY (DoctorId) REFERENCES Users(UserId)
);
GO

CREATE TABLE Posts (
    PostId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    IsAnonymous BIT DEFAULT 0,
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE Comments (
    CommentId INT PRIMARY KEY IDENTITY(1,1),
    PostId INT NOT NULL,
    UserId INT NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    IsAnonymous BIT DEFAULT 0,
    CreatedDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PostId) REFERENCES Posts(PostId),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE INDEX IX_BloodSugarRecords_UserId ON BloodSugarRecords(UserId);
GO

CREATE INDEX IX_BloodSugarRecords_RecordDate ON BloodSugarRecords(RecordDate);
GO

CREATE INDEX IX_WoundRecords_UserId ON WoundRecords(UserId);
GO

CREATE INDEX IX_ConsultationMessages_SenderId ON ConsultationMessages(SenderId);
GO

CREATE INDEX IX_ConsultationMessages_ReceiverId ON ConsultationMessages(ReceiverId);
GO

CREATE INDEX IX_Reminders_UserId ON Reminders(UserId);
GO

INSERT INTO Users (Username, Email, PasswordHash, FullName, UserType, IsActive, CreatedDate)
VALUES ('wangwu', 'wangwu@example.com', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', N'wang Doctor', N'Doctor', 1, GETDATE());
GO

INSERT INTO DoctorProfiles (UserId, Department, ProfessionalTitle, HospitalName, Specialty, Introduction, ConsultationHours, ClinicAddress, CreatedDate)
SELECT UserId, N'糖尿病足专科', N'副主任医师', N'糖尿病专科医院', N'糖尿病足筛查、创面管理', N'wang Doctor 医生资料', N'周二至周六 09:00-18:00', N'门诊楼 2 层糖尿病专科', GETDATE()
FROM Users
WHERE Username = 'wangwu';
GO

INSERT INTO Users (Username, Email, PasswordHash, FullName, UserType, IsActive, CreatedDate)
VALUES ('lisi', 'lisi@example.com', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', N'li Doctor', N'Doctor', 1, GETDATE());
GO

INSERT INTO DoctorProfiles (UserId, Department, ProfessionalTitle, HospitalName, Specialty, Introduction, ConsultationHours, ClinicAddress, CreatedDate)
SELECT UserId, N'内分泌科', N'主治医师', N'糖尿病专科医院', N'血糖管理、慢病随访', N'li Doctor 医生资料', N'周一至周五 08:00-17:00', N'门诊楼 2 层糖尿病专科', GETDATE()
FROM Users
WHERE Username = 'lisi';
GO

INSERT INTO Users (Username, Email, PasswordHash, FullName, UserType, IsActive, CreatedDate)
VALUES ('zhangsan', 'zhangsan@example.com', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', N'zhang Doctor', N'Doctor', 1, GETDATE());
GO

INSERT INTO DoctorProfiles (UserId, Department, ProfessionalTitle, HospitalName, Specialty, Introduction, ConsultationHours, ClinicAddress, CreatedDate)
SELECT UserId, N'糖尿病足专科', N'副主任医师', N'糖尿病专科医院', N'糖尿病足筛查、创面管理', N'zhang Doctor 医生资料', N'周二至周六 09:00-18:00', N'门诊楼 2 层糖尿病专科', GETDATE()
FROM Users
WHERE Username = 'zhangsan';
GO

