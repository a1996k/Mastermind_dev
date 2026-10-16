using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public enum ApplicationScope
    {
        Global = 1,
        PurchaseOrders = 2
    }

    public enum UserFlags
    {
        IsSpecialApprover,
        IsProductionEmployee,
        IsProjectManager,
        IsQAChecker
    }

    public enum HostingEnvironment
    {
        Staging = 1,
        Live = 2
    }

    public enum SearchDateReference
    {
        Created,
        Completed
    }

    public enum RecordType
    {
        Quote,
        Job
    }

    public enum RangeOverride
    {
        Days = 1,
        Weeks = 2, //not yet implemented
        Months = 3,
        Years = 4,
        None = 0
    }

    public enum GlobalUserRoles
    {
        User = 1,
        CSR = 2,
        Admin = 3
    }

    public enum SelectListType
    {
        PriceLevel,
        Clients,
        FormatUnitPrice,
        ServiceCategories,
        MaterialCategories,
        PressCategories,
        SpeedDenominators,
        InkCostDenominatorUnit,
        Roles,
        ServiceTypes,
        ConstraintTypes,
        ConstraintValueTypes,
        Users,
        TimeTrackingViewBy,
        Formulas,
        USStates,
        StockType,
        CostType,
        ShippingOptions,
        Vendors,
        ProductionDepartments,
        CustomerServiceReps,
        FinishingFormulaApplications,
        FinishingMaterialFormulas,
        Environments,
        Modules,
        EmployeeDepartments,
        FinishingServices,
        Tournaments,
        BillingClasses
    }

    public enum SpeedDenominators
    {
        SheetsPerHour = 1,
        RollsPerHour = 2,
        SqFtPerHour = 3,
        FtPerHour = 4,
        None = 5
    }

    //not working
    public enum ConstraintEnabledEntities
    {
        Press
    }

    public enum DashboardCards
    {
        TopClients = 1,
        CompletedJobSummary = 2,
        Quotes = 3,
        JobCompletionMetrics = 4,
        ResourcesAvailable = 5,
        CostToOrganization = 6,
        Utilization = 7,
        Margin = 8,
        TimeTracking = 13,
        AgencyUtilization = 14,
        FullPrintUtilization = 17
    }

    public enum DepUtzOverallLeadershipUsers
    {
        JustinWebb = 7,
        MarcusDiedrich = 11
    }

    public enum DepUtzAgencyLeadershipUsers
    {
        AshleyVogel = 12
    }

    public enum DepUtzPrintLeadershipUsers
    {
        JasonKuhnz = 6,
        CalianSchrader = 20
    }

    public enum TournamentUserRoles
    {
        Admin = 1,
        Manager = 2
    }
}