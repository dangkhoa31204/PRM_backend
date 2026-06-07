-- ============================================================
-- MIGRATE LOCAL DATA TO AZURE SQL DATABASE
-- ============================================================

-- 1. Accounts
PRINT 'Migrating Accounts...';
SET IDENTITY_INSERT [dbo].[Accounts] ON;
MERGE INTO [dbo].[Accounts] AS Target
USING (VALUES
    (1, N'admin', N'admin@qrorder.com', N'123456', N'System Administrator', NULL, 1, 1, '2026-06-05 12:58:54.9966667', NULL)
) AS Source (AccountId, Username, Email, PasswordHash, FullName, PhoneNumber, Role, IsActive, CreatedAt, LastLoginAt)
ON Target.AccountId = Source.AccountId
WHEN NOT MATCHED THEN
    INSERT (AccountId, Username, Email, PasswordHash, FullName, PhoneNumber, Role, IsActive, CreatedAt, LastLoginAt)
    VALUES (Source.AccountId, Source.Username, Source.Email, Source.PasswordHash, Source.FullName, Source.PhoneNumber, Source.Role, Source.IsActive, Source.CreatedAt, Source.LastLoginAt);
SET IDENTITY_INSERT [dbo].[Accounts] OFF;
PRINT 'Accounts done.';

-- 2. MenuItems
PRINT 'Migrating MenuItems...';
SET IDENTITY_INSERT [dbo].[MenuItems] ON;
MERGE INTO [dbo].[MenuItems] AS Target
USING (VALUES
    (1, N'Espresso',     N'Italian espresso',    30000.00, 1, NULL, 1, '2026-06-05 08:46:39.390', NULL),
    (2, N'Latte',        N'Milk coffee',          45000.00, 1, NULL, 1, '2026-06-05 08:46:39.390', NULL),
    (3, N'Matcha Tea',   N'Japanese matcha',      50000.00, 2, NULL, 1, '2026-06-05 08:46:39.390', NULL),
    (4, N'Cheesecake',   N'New York cheesecake',  55000.00, 3, NULL, 1, '2026-06-05 08:46:39.390', NULL),
    (5, N'Orange Juice', N'Fresh orange juice',   40000.00, 4, NULL, 1, '2026-06-05 08:46:39.390', NULL)
) AS Source (MenuItemId, Name, Description, Price, Category, ImageUrl, IsAvailable, CreatedAt, UpdatedAt)
ON Target.MenuItemId = Source.MenuItemId
WHEN NOT MATCHED THEN
    INSERT (MenuItemId, Name, Description, Price, Category, ImageUrl, IsAvailable, CreatedAt, UpdatedAt)
    VALUES (Source.MenuItemId, Source.Name, Source.Description, Source.Price, Source.Category, Source.ImageUrl, Source.IsAvailable, Source.CreatedAt, Source.UpdatedAt);
SET IDENTITY_INSERT [dbo].[MenuItems] OFF;
PRINT 'MenuItems done.';


-- 3. Tables
PRINT 'Migrating Tables...';
SET IDENTITY_INSERT [dbo].[Tables] ON;
MERGE INTO [dbo].[Tables] AS Target
USING (VALUES
    (1, 4, 1, '2026-06-05 08:46:39.386'),
    (2, 4, 1, '2026-06-05 08:46:39.386'),
    (3, 4, 1, '2026-06-05 08:46:39.386'),
    (4, 4, 1, '2026-06-05 08:46:39.386'),
    (5, 6, 1, '2026-06-05 08:46:39.386'),
    (6, 6, 1, '2026-06-05 08:46:39.386'),
    (7, 8, 1, '2026-06-05 08:46:39.386'),
    (8, 8, 1, '2026-06-05 08:46:39.386')
) AS Source (TableId, Capacity, Status, CreatedAt)
ON Target.TableId = Source.TableId
WHEN NOT MATCHED THEN
    INSERT (TableId, Capacity, Status, CreatedAt)
    VALUES (Source.TableId, Source.Capacity, Source.Status, Source.CreatedAt);
SET IDENTITY_INSERT [dbo].[Tables] OFF;
PRINT 'Tables done.';

PRINT '=== Migration completed successfully! ===';
