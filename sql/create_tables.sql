-- SQL Server schema for notifications system

CREATE TABLE Notifications (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    Link NVARCHAR(500) NULL,
    FileName NVARCHAR(260) NULL,
    FileUrl NVARCHAR(2048) NULL,
    FileBase64 NVARCHAR(MAX) NULL,
    TargetVersion NVARCHAR(50) NULL,
    TimestampUtc DATETIME2 NOT NULL
);

CREATE TABLE Devices (
    DeviceId NVARCHAR(100) PRIMARY KEY,
    CardCode NVARCHAR(100) NULL,
    CurrentVersion NVARCHAR(50) NULL,
    LastSeen DATETIME2 NOT NULL
);

CREATE TABLE DeviceNotifications (
    DeviceId NVARCHAR(100) NOT NULL,
    NotificationId UNIQUEIDENTIFIER NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    ReadAt DATETIME2 NULL,
    PRIMARY KEY (DeviceId, NotificationId),
    FOREIGN KEY (DeviceId) REFERENCES Devices(DeviceId) ON DELETE CASCADE,
    FOREIGN KEY (NotificationId) REFERENCES Notifications(Id) ON DELETE CASCADE
);

