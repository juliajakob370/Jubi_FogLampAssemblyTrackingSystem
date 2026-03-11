/*
 * FILE          : P01_ConfigTableCreation&InitialValues
 * PROJECT       : P01 - Configuration Table Creation
 * PROGRAMMER    : Julia Jakob  & Bibi Murwared
 * DESCRIPTION   : Defines a stored procedure that clears the Configuration table and reloads it with the default simulation and bin settings.
 */
GO

CREATE OR ALTER PROCEDURE ResetConfigurationToDefaults
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM Configuration;

    INSERT INTO Configuration (ConfigName, ConfigValue, DataType, Description) VALUES
     -- Insert Bin Capacities ------------------------
     ('Harness.Capacity', '55', 'int', 'Bin capacity for harnesses'),
     ('Reflector.Capacity', '35', 'int', 'Bin capacity for reflectors'),
     ('Housing.Capacity', '24', 'int', 'Bin capacity for housings'),
     ('Lens.Capacity', '40', 'int', 'Bin capacity for lenses'),
     ('Bulb.Capacity', '60', 'int', 'Bin capacity for bulbs'),
     ('Bezel.Capacity', '75', 'int', 'Bin capacity for bezels'),

     -- Factory / Simulation Configurations -------------------------------
     ('LowStockThreshold', '5', 'int', 'Number of parts threshold to flag as low quantity'),
     ('TotalOrderQuantity', '500', 'int', 'Total number of lamps to build'),
     ('NumberOfWorkStations', '3', 'int', 'Number of work stations in the assembly area'),
     ('SimulationTimeScale', '1.0', 'decimal', 'Time scale for simulation'),

     -- Worker Time Configuration Values ----------------------------------------------
     ('BaseTime', '60.0','decimal','Base time for how fast it takes to assemble a completed fog lamp in seconds'),
     ('ExperiencedWorker.TimeVariationPercentage', '10.0','decimal','An experienced worker can assemble a completed fog lamp in the base time +/- this percentage'),
     ('NewEmployee.TimeVariationPercentage', '50.0','decimal','A new worker takes this percentage longer than the base time to complete a fog lamp'),
     ('SuperWorker.TimeVariationPercentage', '15.0','decimal','A super worker can complete a fog lamp in this percentage less time than the base time'),

     -- Worker Defect Rates -----------------------------------------------------------
     ('ExperiencedWorker.DefectRate', '0.5','decimal','A normal/experienced worker has this defect percentage'),
     ('NewEmployee.DefectRate', '0.85','decimal','A new worker has this defect percentage'),
     ('SuperWorker.DefectRate', '0.15','decimal','A super/ very experiencedworker has this defect percentage');
END;
