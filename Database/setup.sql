-- =============================================
-- Widget Desktop App - Database Setup Script
-- =============================================

-- Create Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'WidgetDb')
BEGIN
    CREATE DATABASE WidgetDb;
END
GO

USE WidgetDb;
GO

-- =============================================
-- Create Tables
-- =============================================

-- Users Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Username NVARCHAR(50) NOT NULL UNIQUE,
        Password NVARCHAR(255) NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- Todos Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Todos' AND xtype='U')
BEGIN
    CREATE TABLE Todos (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        IsCompleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Todos_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    );
END
GO

-- Stats Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Stats' AND xtype='U')
BEGIN
    CREATE TABLE Stats (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL UNIQUE,
        TotalTasks INT NOT NULL DEFAULT 0,
        CompletedTasks INT NOT NULL DEFAULT 0,
        LastLogin DATETIME2 NULL,
        CONSTRAINT FK_Stats_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    );
END
GO

-- =============================================
-- Seed Data
-- =============================================

-- Insert default users (password should be hashed in production!)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, Password, Name) VALUES ('admin', 'admin123', 'Admin User');
    INSERT INTO Users (Username, Password, Name) VALUES ('demo', 'demo123', 'Demo User');

    -- Stats for users
    INSERT INTO Stats (UserId, TotalTasks, CompletedTasks, LastLogin) VALUES (1, 3, 1, GETUTCDATE());
    INSERT INTO Stats (UserId, TotalTasks, CompletedTasks, LastLogin) VALUES (2, 2, 0, GETUTCDATE());

    -- Sample todos for admin
    INSERT INTO Todos (UserId, Title, IsCompleted) VALUES (1, 'Setup project architecture', 1);
    INSERT INTO Todos (UserId, Title, IsCompleted) VALUES (1, 'Build REST API', 0);
    INSERT INTO Todos (UserId, Title, IsCompleted) VALUES (1, 'Design dashboard UI', 0);

    -- Sample todos for demo
    INSERT INTO Todos (UserId, Title, IsCompleted) VALUES (2, 'Learn WPF basics', 0);
    INSERT INTO Todos (UserId, Title, IsCompleted) VALUES (2, 'Connect API to widget', 0);
END
GO

PRINT 'Database setup complete!';
PRINT 'Default users: admin/admin123, demo/demo123';
GO
