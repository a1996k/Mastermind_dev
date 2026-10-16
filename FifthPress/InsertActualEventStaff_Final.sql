-- Insert Actual Event Staff Members from fcevents.csv into Users table
-- Only inserts users that don't already exist (checking by UPPER FirstName + LastName)
-- Using correct table structure and defaults for User table

-- First, let's see which users already exist
PRINT 'Checking for existing users...';
PRINT '';

SELECT 
    FirstName,
    LastName,
    Username,
    Login,
    IsEventStaff
FROM Users
WHERE Deleted IS NULL
    AND (
        (UPPER(FirstName) = 'JUSTIN' AND UPPER(LastName) = 'WEBB') OR
        (UPPER(FirstName) = 'CASEY' AND UPPER(LastName) = 'CARR') OR
        (UPPER(FirstName) = 'JEFF' AND UPPER(LastName) = 'SIREK') OR
        (UPPER(FirstName) = 'NICK' AND UPPER(LastName) = 'FRANZEN') OR
        (UPPER(FirstName) = 'AUSTIN' AND UPPER(LastName) = 'SANDERSON') OR
        (UPPER(FirstName) = 'BROOKS' AND UPPER(LastName) = 'HAAS') OR
        (UPPER(FirstName) = 'BRAD' AND UPPER(LastName) = 'SEGURA') OR
        (UPPER(FirstName) = 'BEN' AND UPPER(LastName) = 'BOHR') OR
        (UPPER(FirstName) = 'ALEC' AND UPPER(LastName) = 'BAILEY') OR
        (UPPER(FirstName) = 'RANDY' AND UPPER(LastName) = 'ARMITAGE') OR
        (UPPER(FirstName) = 'PATRICK' AND UPPER(LastName) = 'JEFFORD') OR
        (UPPER(FirstName) = 'KEVIN' AND UPPER(LastName) = 'KERNAHAN') OR
        (UPPER(FirstName) = 'ED' AND UPPER(LastName) = 'CRAWFORD') OR
        (UPPER(FirstName) = 'AXEL' AND UPPER(LastName) = 'STUMPHY') OR
        (UPPER(FirstName) = 'ABE' AND UPPER(LastName) = 'SARMIENTO') OR
        (UPPER(FirstName) = 'KYLE' AND UPPER(LastName) = 'KLUMP') OR
        (UPPER(FirstName) = 'JAGGAR' AND UPPER(LastName) = 'MOORE') OR
        (UPPER(FirstName) = 'TRISTIN' AND UPPER(LastName) = 'WESTPHAL') OR
        (UPPER(FirstName) = 'DASHAUN' AND UPPER(LastName) = 'WESTPHAL') OR
        (UPPER(FirstName) = 'MICHAEL' AND UPPER(LastName) = 'SMITH') OR
        (UPPER(FirstName) = 'CARTER' AND UPPER(LastName) = 'LEIBHAM') OR
        (UPPER(FirstName) = 'MAX' AND UPPER(LastName) = 'LEIBHAM') OR
        (UPPER(FirstName) = 'KEN' AND UPPER(LastName) = 'FOUSE') OR
        (UPPER(FirstName) = 'ALEX' AND UPPER(LastName) = 'BRIDGE') OR
        (UPPER(FirstName) = 'JAKE' AND UPPER(LastName) = 'WEIL') OR
        (UPPER(FirstName) = 'ALEXANDER' AND UPPER(LastName) = 'LOPEZ') OR
        (UPPER(FirstName) = 'KADEN' AND UPPER(LastName) = 'CHARBONEUA') OR
        (UPPER(FirstName) = 'TRASHAUN' AND UPPER(LastName) = 'WILLIS') OR
        (UPPER(FirstName) = 'AARON' AND UPPER(LastName) = 'BOONE') OR
        (UPPER(FirstName) = 'LOGAN' AND UPPER(LastName) = 'SMITH') OR
        (UPPER(FirstName) = 'GREGORY' AND UPPER(LastName) = 'WITTKOPP') OR
        (UPPER(FirstName) = 'GAVIN' AND UPPER(LastName) = 'CASEY') OR
        (UPPER(FirstName) = 'BLAINE' AND UPPER(LastName) = 'KETCHUM') OR
        (UPPER(FirstName) = 'DEALLO' AND UPPER(LastName) = 'WALKIN') OR
        (UPPER(FirstName) = 'JULIA' AND UPPER(LastName) = 'NINO') OR
        (UPPER(FirstName) = 'KEAN' AND UPPER(LastName) = 'O''NEIL') OR
        (UPPER(FirstName) = 'AUSTIN' AND UPPER(LastName) = 'GOMEZ') OR
        (UPPER(FirstName) = 'KARTER' AND UPPER(LastName) = 'GUZMAN') OR
        (UPPER(FirstName) = 'AIDAN' AND UPPER(LastName) = 'DOUGHTY') OR
        (UPPER(FirstName) = 'PEYTON' AND UPPER(LastName) = 'WEISROCK') OR
        (UPPER(FirstName) = 'ETHAN' AND UPPER(LastName) = 'VERHULST') OR
        (UPPER(FirstName) = 'FLETCHER' AND UPPER(LastName) = 'DUNLAP') OR
        (UPPER(FirstName) = 'TYLER' AND UPPER(LastName) = 'PHILLIPS') OR
        (UPPER(FirstName) = 'ALEC' AND UPPER(LastName) = 'WEIL') OR
        (UPPER(FirstName) = 'CHASE' AND UPPER(LastName) = 'WETZLER') OR
        (UPPER(FirstName) = 'DEVON' AND UPPER(LastName) = 'LAURENCIN') OR
        (UPPER(FirstName) = 'NOAH' AND UPPER(LastName) = 'DEIBERT') OR
        (UPPER(FirstName) = 'ALONSO' AND UPPER(LastName) = 'SARMIENTO') OR
        (UPPER(FirstName) = 'BRYAN' AND UPPER(LastName) = 'SCHAEFER') OR
        (UPPER(FirstName) = 'MARCUS' AND UPPER(LastName) = 'DIEDRICH') OR
        (UPPER(FirstName) = 'BRAYDEN' AND UPPER(LastName) = 'SCHRADER') OR
        (UPPER(FirstName) = 'KUJTIM' AND UPPER(LastName) = 'QUNAJ') OR
        (UPPER(FirstName) = 'ARRICK' AND UPPER(LastName) = 'PARKS') OR
        (UPPER(FirstName) = 'GARRETT' AND UPPER(LastName) = 'WILTERDINK') OR
        (UPPER(FirstName) = 'JASON' AND UPPER(LastName) = 'KUHNZ') OR
        (UPPER(FirstName) = 'MICHAEL' AND UPPER(LastName) = 'KAAT') OR
        (UPPER(FirstName) = 'JOSH' AND UPPER(LastName) = 'KONSEK') OR
        (UPPER(FirstName) = 'NATHAN' AND UPPER(LastName) = 'ADER') OR
        (UPPER(FirstName) = 'KEN' AND UPPER(LastName) = 'FOURNIER') OR
        (UPPER(FirstName) = 'CHARLIE' AND UPPER(LastName) = 'LEIBHAM') OR
        (UPPER(FirstName) = 'CHRIS' AND UPPER(LastName) = 'MOUSHON') OR
        (UPPER(FirstName) = 'ISAIAH' AND UPPER(LastName) = 'LAWRYN') OR
        (UPPER(FirstName) = 'NICHOLAS' AND UPPER(LastName) = 'IVORY')
    );

PRINT '';
PRINT 'Starting user import/update process...';
PRINT '';

-- Create temp table to hold staff data with calculated fields
CREATE TABLE #TempEventStaff (
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    Username NVARCHAR(100),
    Login NVARCHAR(50),
    Passcode NVARCHAR(50),
    RowNum INT
);

-- Insert staff data with calculated Username and Passcode
INSERT INTO #TempEventStaff (FirstName, LastName, Username, Passcode, RowNum)
VALUES
    ('Justin', 'Webb', 'JustinW', 'JW2024', 1),
    ('Casey', 'Carr', 'CaseyC', 'CC2024', 2),
    ('Jeff', 'Sirek', 'JeffS', 'JS2024', 3),
    ('Nick', 'Franzen', 'NickF', 'NF2024', 4),
    ('Austin', 'Sanderson', 'AustinS', 'AS2024', 5),
    ('Brooks', 'Haas', 'BrooksH', 'BH2024', 6),
    ('Brad', 'Segura', 'BradS', 'BS2024', 7),
    ('Ben', 'Bohr', 'BenB', 'BB2024', 8),
    ('Alec', 'Bailey', 'AlecB', 'AB2024', 9),
    ('Randy', 'Armitage', 'RandyA', 'RA2024', 10),
    ('Patrick', 'Jefford', 'PatrickJ', 'PJ2024', 11),
    ('Kevin', 'Kernahan', 'KevinK', 'KK2024', 12),
    ('Ed', 'Crawford', 'EdC', 'EC2024', 13),
    ('Axel', 'Stumphy', 'AxelS', 'AS2024', 14),
    ('Abe', 'Sarmiento', 'AbeS', 'AS2024', 15),
    ('Kyle', 'Klump', 'KyleK', 'KK2024', 16),
    ('Jaggar', 'Moore', 'JaggarM', 'JM2024', 17),
    ('Tristin', 'Westphal', 'TristinW', 'TW2024', 18),
    ('Dashaun', 'Westphal', 'DashaunW', 'DW2024', 19),
    ('Michael', 'Smith', 'MichaelS', 'MS2024', 20),
    ('Carter', 'Leibham', 'CarterL', 'CL2024', 21),
    ('Max', 'Leibham', 'MaxL', 'ML2024', 22),
    ('Ken', 'Fouse', 'KenF', 'KF2024', 23),
    ('Alex', 'Bridge', 'AlexB', 'AB2024', 24),
    ('Jake', 'Weil', 'JakeW', 'JW2024', 25),
    ('Alexander', 'Lopez', 'AlexanderL', 'AL2024', 26),
    ('Kaden', 'Charboneua', 'KadenC', 'KC2024', 27),
    ('Trashaun', 'Willis', 'TrashaunW', 'TW2024', 28),
    ('Aaron', 'Boone', 'AaronB', 'AB2024', 29),
    ('Logan', 'Smith', 'LoganS', 'LS2024', 30),
    ('Gregory', 'Wittkopp', 'GregoryW', 'GW2024', 31),
    ('Gavin', 'Casey', 'GavinC', 'GC2024', 32),
    ('Blaine', 'Ketchum', 'BlaineK', 'BK2024', 33),
    ('Deallo', 'Walkin', 'DealloW', 'DW2024', 34),
    ('Julia', 'Nino', 'JuliaN', 'JN2024', 35),
    ('Kean', 'O''Neil', 'KeanO', 'KO2024', 36),
    ('Austin', 'Gomez', 'AustinG', 'AG2024', 37),
    ('Karter', 'Guzman', 'KarterG', 'KG2024', 38),
    ('Aidan', 'Doughty', 'AidanD', 'AD2024', 39),
    ('Peyton', 'Weisrock', 'PeytonW', 'PW2024', 40),
    ('Ethan', 'Verhulst', 'EthanV', 'EV2024', 41),
    ('Fletcher', 'Dunlap', 'FletcherD', 'FD2024', 42),
    ('Tyler', 'Phillips', 'TylerP', 'TP2024', 43),
    ('Alec', 'Weil', 'AlecW', 'AW2024', 44),
    ('Chase', 'Wetzler', 'ChaseW', 'CW2024', 45),
    ('Devon', 'Laurencin', 'DevonL', 'DL2024', 46),
    ('Noah', 'Deibert', 'NoahD', 'ND2024', 47),
    ('Alonso', 'Sarmiento', 'AlonsoS', 'AS2024', 48),
    ('Bryan', 'Schaefer', 'BryanS', 'BS2024', 49),
    ('Marcus', 'Diedrich', 'MarcusD', 'MD2024', 50),
    ('Brayden', 'Schrader', 'BraydenS', 'BS2024', 51),
    ('Kujtim', 'Qunaj', 'KujtimQ', 'KQ2024', 52),
    ('Arrick', 'Parks', 'ArrickP', 'AP2024', 53),
    ('Garrett', 'Wilterdink', 'GarrettW', 'GW2024', 54),
    ('Casey', 'Carr2', 'CaseyC2', 'CC2024', 55), -- Second Casey Carr (differentiated)
    ('Jason', 'Kuhnz', 'JasonK', 'JK2024', 56),
    ('Michael', 'Kaat', 'MichaelK', 'MK2024', 57),
    ('Josh', 'Konsek', 'JoshK', 'JK2024', 58),
    ('Nathan', 'Ader', 'NathanA', 'NA2024', 59),
    ('Ken', 'Fournier', 'KenF2', 'KF2024', 60), -- Second Ken (differentiated)
    ('Charlie', 'Leibham', 'CharlieL', 'CL2024', 61),
    ('Chris', 'Moushon', 'ChrisM', 'CM2024', 62),
    ('Isaiah', 'Lawryn', 'IsaiahL', 'IL2024', 63),
    ('Nicholas', 'Ivory', 'NicholasI', 'NI2024', 64);

-- Calculate Login numbers starting from next available after 12000
DECLARE @MaxExistingLogin INT;
SELECT @MaxExistingLogin = ISNULL(MAX(CAST(Login AS INT)), 11999)
FROM Users 
WHERE ISNUMERIC(Login) = 1 AND CAST(Login AS INT) >= 12000;

-- Update temp table with Login numbers
UPDATE #TempEventStaff
SET Login = CAST(@MaxExistingLogin + RowNum AS NVARCHAR(50));

-- Now merge the data into Users table
-- Only insert if user doesn't exist (checking by UPPER FirstName + LastName)
MERGE Users AS target
USING #TempEventStaff AS source
ON UPPER(target.FirstName) = UPPER(source.FirstName) 
   AND UPPER(target.LastName) = UPPER(source.LastName)
   AND target.Deleted IS NULL
WHEN NOT MATCHED THEN
    INSERT (
        FirstName, 
        LastName, 
        Username,
        Login,
        Role,
        Deleted,
        Passcode,
        Disabled,
        Email,
        IsSpecialApprover,
        Salt,
        Hash,
        DashboardPermissions,
        IsProjectManager,
        IsProductionEmployee,
        IsExecutive,
        IsQAChecker,
        AgencyUtilizationApplicable,
        TargetUtilization,
        AgencyDepartment,
        IsEventStaff
    )
    VALUES (
        source.FirstName,
        source.LastName,
        source.Username,
        source.Login,
        1,                              -- Role = 1
        NULL,                           -- Deleted = NULL
        source.Passcode,                -- Passcode (first initial + last initial + 2024)
        0,                              -- Disabled = 0
        NULL,                           -- Email = NULL
        0,                              -- IsSpecialApprover = 0
        'TKhb+ylkWsMH7ldcd03HoQ==',    -- Salt (standard value provided)
        'jx0kcnOUGSEBCLHv8RnWkuN7T0c=', -- Hash (standard value provided)
        NULL,                           -- DashboardPermissions = NULL
        0,                              -- IsProjectManager = 0
        0,                              -- IsProductionEmployee = 0
        0,                              -- IsExecutive = 0
        0,                              -- IsQAChecker = 0
        0,                              -- AgencyUtilizationApplicable = 0
        NULL,                           -- TargetUtilization = NULL
        NULL,                           -- AgencyDepartment = NULL
        1                              -- IsEventStaff = 1
    );

-- Track what was done
DECLARE @InsertedCount INT = @@ROWCOUNT;

-- Clean up temp table
DROP TABLE #TempEventStaff;
