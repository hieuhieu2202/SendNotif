-- SQL schema for notifications system
CREATE TABLE IF NOT EXISTS Notifications (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    Body TEXT NOT NULL,
    Link TEXT,
    FileName TEXT,
    FileUrl TEXT,
    FileBase64 TEXT,
    TargetVersion TEXT,
    TimestampUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Devices (
    DeviceId TEXT PRIMARY KEY,
    CurrentVersion TEXT,
    LastSeen TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS DeviceNotifications (
    DeviceId TEXT NOT NULL,
    NotificationId TEXT NOT NULL,
    Status TEXT NOT NULL,
    ReadAt TEXT,
    PRIMARY KEY (DeviceId, NotificationId),
    FOREIGN KEY (DeviceId) REFERENCES Devices(DeviceId) ON DELETE CASCADE,
    FOREIGN KEY (NotificationId) REFERENCES Notifications(Id) ON DELETE CASCADE
);
