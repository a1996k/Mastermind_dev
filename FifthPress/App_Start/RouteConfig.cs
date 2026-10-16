using OfficeOpenXml.ConditionalFormatting.Contracts;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;

namespace FifthPress
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            /*
            routes.MapRoute(
                name: "",
                url: "",
                defaults: new { controller = "", action = "" }
            );
            */

            routes.MapRoute(
                name: "ClientPortalVerify",
                url: "ClientPortal/Verify/{token}",
                defaults: new { controller = "ClientPortal", action = "Verify", token = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EventBudget",
                url: "ClientPortal/GetEventBudget/{eventId}",
                defaults: new { controller = "ClientPortal", action = "GetEventBudget", eventId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ClientPortalDownloadProof",
                url: "ClientPortal/DownloadProof/{proofId}",
                defaults: new { controller = "ClientPortal", action = "DownloadProof", proofId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "VerifyClientAccess",
                url: "ClientPortal/VerifyClientAccess",
                defaults: new { controller = "ClientPortal", action = "VerifyClientAccess" }
            );

            routes.MapRoute(
                name: "GetProofingJobsSigned",
                url: "ClientPortal/GetProofingJobsSigned",
                defaults: new { controller = "ClientPortal", action = "GetProofingJobsSigned" }
            );

            routes.MapRoute(
                name: "GetProofingJobDetailsSigned",
                url: "ClientPortal/GetProofingJobDetailsSigned",
                defaults: new { controller = "ClientPortal", action = "GetProofingJobDetailsSigned" }
            );

            routes.MapRoute(
                name: "DownloadProofSigned",
                url: "ClientPortal/DownloadProofSigned/{proofId}",
                defaults: new { controller = "ClientPortal", action = "DownloadProofSigned", proofId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "UpdateProofStatusSigned",
                url: "ClientPortal/UpdateProofStatusSigned",
                defaults: new { controller = "ClientPortal", action = "UpdateProofStatusSigned" }
            );

            routes.MapRoute(
                name: "TestPOEscalation",
                url: "test-po-escalation",
                defaults: new { controller = "PO25Admin", action = "TestEscalation" }
            );

            routes.MapRoute(
                name: "ViewQuoteLayout2",
                url: "view-quote-layout2/{quoteItemId}",
                defaults: new { controller = "JobLayout", action = "ViewQuoteLayout", quoteItemId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ViewJobLayout2",
                url: "view-job-layout2/{jobItemId}",
                defaults: new { controller = "JobLayout", action = "ViewJobLayout", jobItemId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "PurchaseOrderSearch",
                url: "purchase-order-search",
                defaults: new { controller = "Search", action = "PurchaseOrderSearch" }
            );

            routes.MapRoute(
                name: "PurchaseOrderSearchIndex",
                url: "PurchaseOrderSearch",
                defaults: new { controller = "Search", action = "PurchaseOrderSearchIndex" }
            );

            routes.MapRoute(
                name: "FreightReport",
                url: "billing/reports/freight-report/{clients}",
                defaults: new { controller = "Billing", action = "FreightReport", clients = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "BillingReports",
                url: "billing/reports",
                defaults: new { controller = "Billing", action = "ReportsIndex" }
            );

            routes.MapRoute(
                name: "RemovePrice",
                url: "remove-price",
                defaults: new { controller = "Pricing", action = "RemovePrice" }
            );

            routes.MapRoute(
                name: "VacationSchedule",
                url: "vacation-schedule",
                defaults: new { controller = "Administration", action = "VacationSchedule" }
            );

            routes.MapRoute(
                name: "GetInks",
                url: "get-inks",
                defaults: new { controller = "Material", action = "GetInks" }
            );

            routes.MapRoute(
                name: "CancelJob",
                url: "cancel-job",
                defaults: new { controller = "Job2", action = "CancelJob" }
            );

            routes.MapRoute(
                name: "DeleteJobDocs",
                url: "delete-job-docs",
                defaults: new { controller = "Job3", action = "DeleteJobDocuments" }
            );

            routes.MapRoute(
                name: "DeleteQuoteDocs",
                url: "delete-quote-docs",
                defaults: new { controller = "Job3", action = "DeleteQuoteDocuments" }
            );

            routes.MapRoute(
                name: "DownloadProofOld",
                url: "download-proof/{proofId}",
                defaults: new { controller = "File", action = "DownloadProof", proofId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ViewJobFiles",
                url: "view-item-files/{jobItemId}",
                defaults: new { controller = "File", action = "ViewItemFiles", jobItemId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "MarkShipmentAsShipped",
                url: "mark-shipment-as-shipped",
                defaults: new { controller = "Shipping", action = "MarkShipmentAsShipped" }
            );

            routes.MapRoute(
                name: "AddShipment",
                url: "add-shipment",
                defaults: new { controller = "Shipping", action = "SaveJobShipment" }
            );

            routes.MapRoute(
                name: "NewJobShipment",
                url: "new-job-shipment",
                defaults: new { controller = "Shipping", action = "NewJobShipment" }
            );

            routes.MapRoute(
                name: "JobShipmentBundling",
                url: "job-shipments",
                defaults: new { controller = "Shipping", action = "BundleJobShipments_Index" }
            );

            routes.MapRoute(
                name: "SubmitOrderRequest",
                url: "submit-order-request",
                defaults: new { controller = "Order", action = "SubmitOrder" }
            );

            routes.MapRoute(
                name: "GetBaseMaterialPrice",
                url: "get-base-material-price/{materialId}",
                defaults: new { controller = "Material", action = "GetBasePrice", materialId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditTimeEntry",
                url: "TimeTracking/EditTimeEntry/{id}",
                defaults: new { controller = "TimeTracking", action = "EditTimeEntry", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveTimeEdit",
                url: "TimeTracking/SaveTimeEntry",
                defaults: new { controller = "TimeTracking", action = "SaveTimeEdit" }
            );

            routes.MapRoute(
                name: "ShowBilledJobs",
                url: "billing-history",
                defaults: new { controller = "Job", action = "ShowBilledJobs" }
            );

            routes.MapRoute(
                name: "Tournaments",
                url: "Tournaments",
                defaults: new { controller = "Tournament", action = "TournamentsIndex" }
            );

            routes.MapRoute(
                name: "TournamentAddUser",
                url: "Tournaments/AddUser",
                defaults: new { controller = "Tournament", action = "AddUser" }
            );

            routes.MapRoute(
                name: "TournamentDetails",
                url: "Tournament/Details/{id}",
                defaults: new { controller = "Tournament", action = "Details", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentCreate",
                url: "Tournament/Create",
                defaults: new { controller = "Tournament", action = "Create" }
            );

            routes.MapRoute(
                name: "TournamentEdit",
                url: "Tournament/Edit/{id}",
                defaults: new { controller = "Tournament", action = "Edit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentDelete",
                url: "Tournament/Delete/{id}",
                defaults: new { controller = "Tournament", action = "Delete", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentStaffAssignment",
                url: "Tournament/{id}/Staff",
                defaults: new { controller = "Tournament", action = "StaffAssignment", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentSchedule",
                url: "Tournament/{id}/Schedule",
                defaults: new { controller = "Tournament", action = "Schedule", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentScheduleTemplate",
                url: "Tournament/{tournamentId}/Template/Schedule",
                defaults: new { controller = "Tournament", action = "DownloadScheduleTemplate", tournamentId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentValidateSchedule",
                url: "Tournament/{tournamentId}/Validate/Schedule",
                defaults: new { controller = "Tournament", action = "ValidateSchedule", tournamentId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TournamentCosts",
                url: "Tournament/{tournamentId}/Costs",
                defaults: new { controller = "Tournament", action = "GetTournamentCosts", tournamentId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveClientBillingInfo",
                url: "save-client-billing-info",
                defaults: new { controller = "Customers", action = "SaveBillingInfo" }
            );

            routes.MapRoute(
                name: "GetClientBillingDetails",
                url: "get-client-billing-details/{id}",
                defaults: new { controller = "Customers", action = "GetClientBillingDetails", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "PrintPurchaseOrder",
                url: "PurchaseOrder/Print/{id}",
                defaults: new { controller = "PurchaseOrder", action = "Print", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "OpenPullMaterialModal",
                url: "open-pull-material-modal/{jobItem}",
                defaults: new { controller = "Inventory", action = "LoadPullMaterialInfo", jobItem = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "OpenPullFinishingMaterialModal",
                url: "open-pull-finishing-material-modal/{jifmId}",
                defaults: new { controller = "Inventory", action = "LoadFinishingMaterialPullInfo", jifmId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "LoadReorderItemModal",
                url: "reorder-item-modal/{id}/{itemType}",
                defaults: new { controller = "Job", action = "LoadItemReorderModal", id = UrlParameter.Optional, itemType = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "UpdateItemOrder",
                url: "update-item-order",
                defaults: new { controller = "Job", action = "UpdateItemPositions" }
            );

            routes.MapRoute(
                name: "SaveEditedShippedOrders",
                url: "save-edited-shipped-orders",
                defaults: new { controller = "Shipping", action = "SaveEditedShippedOrders" }
            );

            routes.MapRoute(
                name: "GetShippedOrdersForEdit",
                url: "get-shipped-orders-for-edit/{jobId}",
                defaults: new { controller = "Shipping", action = "GetShippedOrdersForEdit" }
            );

            routes.MapRoute(
                name: "MarkJobAsPreBilled",
                url: "mark-job-as-prebilled",
                defaults: new { controller = "Job", action = "MarkAsPrebilled" }
            );

            routes.MapRoute(
                name: "Prebilling",
                url: "PreBilling",
                defaults: new { controller = "Job", action = "GetPrebillJobs" }
            );

            routes.MapRoute(
                name: "MonthlyAgencyUtilization",
                url: "mth-agc",
                defaults: new { controller = "Email", action = "AgencyMonthlyYTDReport" }
            );

            routes.MapRoute(
                name: "MonthlyPrintUtilization",
                url: "mth-prt",
                defaults: new { controller = "Email", action = "ProductionMonthlyYTDReport" }
            );

            routes.MapRoute(
                name: "WeeklyAgencyUtilization",
                url: "wk-agc",
                defaults: new { controller = "Email", action = "AgencyWeeklyYTDReport" }
            );

            routes.MapRoute(
                name: "WeeklyPrintUtilization",
                url: "wk-prt",
                defaults: new { controller = "Email", action = "ProductionWeeklyYTDReport" }
            );

            routes.MapRoute(
                name: "EditOrdersShipped",
                url: "edit-shipped-orders/{jobId}",
                defaults: new { controller = "Shipping", action = "EditShippedOrders", jobId = UrlParameter.Optional }
            );

            //following route is for bundled job item shipments
            routes.MapRoute(
                name: "SaveJobShipment",
                url: "save-job-shipment",
                defaults: new { controller = "Shipping", action = "SaveJobShippedShipment" }
            );

            routes.MapRoute(
                name: "GetShippedShipments",
                url: "get-shipped-shipments/{shipmentId}",
                defaults: new { controller = "Shipping", action = "GetShippedShipments", shipmentId = UrlParameter.Optional }
            );

            //following route is for individual job shipments
            routes.MapRoute(
                name: "SaveOrderShipment",
                url: "save-order-shipment",
                defaults: new { controller = "Shipping", action = "SaveOrderShipment" }
            );

            routes.MapRoute(
                name: "GetShippedOrders",
                url: "get-shipped-orders/{jobId}",
                defaults: new { controller = "Shipping", action = "GetShippedOrders", jobId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SetJobShippingAddress",
                url: "set-address-on-job",
                defaults: new { controller = "Shipping", action = "SetAddressOnJob" }
            );

            routes.MapRoute(
                name: "ShipAddressCheck",
                url: "ship-address-check/{job}",
                defaults: new { controller = "Shipping", action = "JobHasShippingAddress" }
            );

            routes.MapRoute(
                name: "MarkJobAsShipped",
                url: "mark-job-as-shipped",
                defaults: new { controller = "Job", action = "MarkJobAsShipped" }
            );

            routes.MapRoute(
                name: "JobsReadyToShip",
                url: "ReadyToShip",
                defaults: new { controller = "Job", action = "GetReadyToShipJobs" }
            );

            routes.MapRoute(
                name: "TimeTrackingListJobs",
                url: "time-tracking/list-jobs",
                defaults: new { controller = "TimeTracking", action = "ListJobs" }
            );

            routes.MapRoute(
                name: "StartTimeTracking",
                url: "tt-start-task",
                defaults: new { controller = "TimeTracking", action = "StartTask" }
            );

            routes.MapRoute(
                name: "StopTimeTracking",
                url: "tt-stop-task",
                defaults: new { controller = "TimeTracking", action = "StopTask" }
            );

            routes.MapRoute(
                name: "CompleteToggleTimeTracking",
                url: "tt-complete-toggle",
                defaults: new { controller = "TimeTracking", action = "TaskStateToggle" }
            );

            routes.MapRoute(
                name: "SubmitMaterialPull",
                url: "pull-material-complete",
                defaults: new { controller = "TimeTracking", action = "MaterialPullPromptSubmit" }
            );

            routes.MapRoute(
                name: "TimeTrackingCompleteCheck",
                url: "tt-complete-check",
                defaults: new { controller = "TimeTracking", action = "CompletePromptCheck" }
            );

            routes.MapRoute(
                name: "JobTimeTracking",
                url: "time-tracking/{jobId}/{itemId}",
                defaults: new { controller = "TimeTracking", action = "Index", jobId = UrlParameter.Optional, itemId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "gfClientBillingInfo",
                url: "gf-billing-info/{clientId}",
                defaults: new { controller = "Order", action = "GetBillingInfo", clientId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "gfGetShippingAddresses",
                url: "gf-shipping-addresses/{clientId}",
                defaults: new { controller = "Order", action = "GetShippingAddresses", clientId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "OrderRequestForm",
                url: "order-request-form",
                defaults: new { controller = "Order", action = "RequestForm" }
            );
            routes.MapRoute(
                name: "StartServiceCheck",
                url: "start-service-check",
                defaults: new { controller = "TimeTracking", action = "StartServiceCheck" }
            );

            routes.MapRoute(
                name: "TabDashboardCards",
                url: "dashboard-cards-for-tab",
                defaults: new { controller = "Dashboard", action = "GetTabCards" }
            );

            routes.MapRoute(
                name: "NavbarDuoLookup",
                url: "navbar-duo-lookup/{q}",
                defaults: new { controller = "Job", action = "NavbarDuoLookup" }
            );

            routes.MapRoute(
                name: "QAComplete",
                url: "qa-complete",
                defaults: new { controller = "Job", action = "QAComplete" }
            );

            routes.MapRoute(
                name: "GetJobDetailsQA",
                url: "get-existing-qa-job-details",
                defaults: new { controller = "Job", action = "QAJobDetails" }
            );

            routes.MapRoute(
                name: "QAPasswordCheck",
                url: "qa-pw-check",
                defaults: new { controller = "User", action = "QAPasswordCheck" }
            );

            routes.MapRoute(
                name: "SideMenuTimeTrackerLoad",
                url: "sm-tt-load/{jobString}",
                defaults: new { controller = "TimeTracking", action = "SideMenuTimeTrackerLoad" }
            );

            routes.MapRoute(
                name: "DynamicStopService",
                url: "d-stop-service",
                defaults: new { controller = "Services", action = "SideMenuStop" }
            );

            routes.MapRoute(
                name: "testgeolocation",
                url: "test-geo",
                defaults: new { controller = "Dev", action = "LogGeo" }
            );

            routes.MapRoute(
                name: "AddCardPermission",
                url: "add-card-permission/{userId}/{cardId}",
                defaults: new { controller = "DashboardConfig", action = "AddCardPermission", userId = UrlParameter.Optional, cardId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RemoveCardPermission",
                url: "remove-card-permission/{userId}/{cardId}",
                defaults: new { controller = "DashboardConfig", action = "RemoveCardPermission", userId = UrlParameter.Optional, cardId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetUserChartPermissions",
                url: "user-chart-perms/{userId}/{chartId}",
                defaults: new { controller = "DashboardConfig", action = "GetUserChartPermissions", userId = UrlParameter.Optional, chartId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AddUserToCardPermission",
                url: "add-user-to-card/{requestingUserId}/{requestedUserId}/{cardId}",
                defaults: new { controller = "DashboardConfig", action = "AddUserToCardPermission", requestingUserId = UrlParameter.Optional, requestedUserId = UrlParameter.Optional, cardId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RemoveUserFromCardPermission",
                url: "remove-user-from-card/{requestingUserId}/{requestedUserId}/{cardId}",
                defaults: new { controller = "DashboardConfig", action = "RemoveUserFromCardPermission", requestingUserId = UrlParameter.Optional, requestedUserId = UrlParameter.Optional, cardId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetDashboardConfigurationForUser",
                url: "get-dash-config/{userId}",
                defaults: new { controller = "DashboardConfig", action = "GetUserCardAssignments", userId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "DashboardConfigurator",
                url: "dashboard-configurator",
                defaults: new { controller = "DashboardConfig", action = "Index" }
            );

            routes.MapRoute(
                name: "ListBugs",
                url: "list-bugs",
                defaults: new { controller = "ApplicationManagement", action = "ListBugs" }
            );

            routes.MapRoute(
                name: "EditFromQuoteNumber",
                url: "Quote/EditFromQuoteNumber/{quoteNumber}",
                defaults: new { controller = "Job", action = "EditFromQuoteNumber", quoteNumber = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ProdScheduleTvWithFilter",
                url: "tv/{filter}",
                defaults: new { controller = "Public", action = "ProductionSchedule", tv = true, filter = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ProdScheduleTv",
                url: "tv",
                defaults: new { controller = "Public", action = "ProductionSchedule", tv = true }
            );

            routes.MapRoute(
                name: "SubmitBug",
                url: "submit-bug",
                defaults: new { controller = "ApplicationManagement", action = "SubmitBug" }
            );

            routes.MapRoute(
                name: "SubmitFeatureRequest",
                url: "submit-feature",
                defaults: new { controller = "ApplicationManagement", action = "SubmitFeatureRequest" }
            );

            routes.MapRoute(
                name: "FeatureRequest",
                url: "feature-request",
                defaults: new { controller = "ApplicationManagement", action = "NewFeatureRequest" }
            );

            routes.MapRoute(
                name: "BugReport",
                url: "bug-report",
                defaults: new { controller = "ApplicationManagement", action = "NewBugReport" }
            );

            routes.MapRoute(
                name: "JobInvoice",
                url: "job-invoice/{jobId}",
                defaults: new { controller = "Job", action = "GetInvoice2", jobId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetCostSheet",
                url: "job-cost-sheet/{jobId}",
                defaults: new { controller = "Job", action = "GetCostSheet", jobId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AjaxMarkAsBilled",
                url: "ajax-mark-as-billed",
                defaults: new { controller = "Billing", action = "MarkAsBilled" }
            );

            routes.MapRoute(
                name: "LoadJobMetrics",
                url: "LoadJobMetrics/{recordId}",
                defaults: new { controller = "Billing", action = "LoadJobMetrics", recordId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "BillingIndex",
                url: "billing",
                defaults: new { controller = "Billing", action = "Index" }
            );

            /*routes.MapRoute(
                name: "ReadyToShip",
                url: "Job/ReadyToShip",
                defaults: new { controller = "Job", action = "ReadyToShip" }
            );*/

            routes.MapRoute(
                name: "ReadyToBill",
                url: "Job/ReadyToBill",
                defaults: new { controller = "Job", action = "ReadyToBill" }
            );

            routes.MapRoute(
                name: "InProgressJobs",
                url: "Job/InProgress",
                defaults: new { controller = "Job", action = "InProgressJobs" }
            );

            routes.MapRoute(
                name: "ClearSheetOverride",
                url: "clear-sheet-override",
                defaults: new { controller = "Job", action = "ClearSheetOverride" }
            );

            routes.MapRoute(
                name: "MaterialsByCategory",
                url: "load-materials-by-category/{cat}",
                defaults: new { controller = "Material", action = "LoadMaterialsByCategory" }
            );

            routes.MapRoute(
                name: "RecordInspector",
                url: "record-inspector",
                defaults: new { controller = "Administration", action = "RecordInspector" }
            );

            routes.MapRoute(
                name: "UpdateRecordInspector",
                url: "update-record-inspector",
                defaults: new { controller = "Administration", action = "UpdateRecordInspector" }
            );

            routes.MapRoute(
                name: "SetPassword",
                url: "set-password",
                defaults: new { controller = "User", action = "SetPassword" }
            );

            routes.MapRoute(
                name: "UpdateJobMaterialConfig",
                url: "update-material-config",
                defaults: new { controller = "Material", action = "UpdateMaterialConfig" }
            );

            routes.MapRoute(
                name: "GetMaterialConfig",
                url: "get-material-config/{id}/{itemType}",
                defaults: new { Controller = "Job", action = "GetMaterialConfig", id = UrlParameter.Optional, itemType = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ClearShippingAddress",
                url: "clear-shipping-address",
                defaults: new { controller = "Job", action = "ClearShippingAddress" }
            );

            routes.MapRoute(
                name: "ClearFreightAddress",
                url: "clear-freight-address",
                defaults: new { controller = "Job", action = "ClearFreightAddress" }
            );

            routes.MapRoute(
                name: "ClearBlindShippingAddress",
                url: "clear-blind-shipping-address",
                defaults: new { controller = "Job", action = "ClearBlindShippingAddress" }
            );

            routes.MapRoute(
                name: "AddPressToItem",
                url: "add-press-to-item",
                defaults: new { controller = "Job", action = "AddPressToItem" }
            );

            routes.MapRoute(
                name: "ReRenderServiceList",
                url: "get-service-list/{recordtype}/{id}",
                defaults: new { controller = "Services", action = "ReloadServiceListHtml" }
            );

            routes.MapRoute(
                name: "DynamicContactCreation",
                url: "dynamic-contact-create",
                defaults: new { controller = "Customers", action = "DynamicContactCreation" }
            );

            routes.MapRoute(
                name: "DynamicClientCreation",
                url: "dynamic-client-create",
                defaults: new { controller = "Customers", action = "DynamicClientCreation" }
            );

            routes.MapRoute(
                name: "DynamicFreightAddress",
                url: "dynamic-freight-address",
                defaults: new { controller = "Customers", action = "DynamicFreightAddressAddition" }
            );

            routes.MapRoute(
                name: "DynamicShippingAddress",
                url: "dynamic-shipping-address",
                defaults: new { controller = "Customers", action = "DynamicShippingAddressAddition" }
            );

            routes.MapRoute(
                name: "DynamicBlindShippingAddress",
                url: "dynamic-blind-shipping-address",
                defaults: new { controller = "Customers", action = "DynamicBlindShippingAddressAddition" }
            );

            routes.MapRoute(
                name: "UpdateFinishingMaterials",
                url: "update-finishing-materials",
                defaults: new { controller = "Job", action = "UpdateFinishingMaterials" }
            );

            routes.MapRoute(
                name: "GetSelectedFinishingMaterials",
                url: "get-selected-finishing-materials/{itemtype}/{itemId}",
                defaults: new { controller = "Job", action = "GetSelectedFinishingMaterials", itemId = UrlParameter.Optional, itemtype = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RecordSearch",
                url: "record-search",
                defaults: new { controller = "Search", action = "RecordSearch4" }
            );

            routes.MapRoute(
                name: "RecordLookupIndex",
                url: "RecordLookup",
                defaults: new { controller = "Search", action = "Index" }
            );

            routes.MapRoute(
                name: "GetDashboardCard",
                url: "get-dashboard-card",
                defaults: new { controller = "Card", action = "GetDashboardCard" }
            );
            routes.MapRoute(
                  name: "MarcusAdminScreen",
                  url: "marcus-admin",
                  defaults: new { controller = "Administration", action = "MarcusAdminScreen" }
            );
            routes.MapRoute(
                name: "SaveTimeParameters",
                url: "update-timing-parameters",
                defaults: new { controller = "Services", action = "UpdateFormulaConfig" }
            );
            routes.MapRoute(
                name: "PendingSpecialApprovals",
                url: "pending-po-approvals",
                defaults: new { controller = "Email", action = "SendPendingSpecialApprovals" }
            );
            routes.MapRoute(
                name: "JobRecapEmail",
                url: "job-recap-email",
                defaults: new { controller = "Email", action = "SendJobRecap" }
            );
            routes.MapRoute(
                name: "SendUtilizationEmails",
                url: "send-utz-emails",
                defaults: new { controller = "Email", action = "SendPersonalUtilizationEmails" }
            );
            //test route vvv meant to be changed for email testing
            routes.MapRoute(
                name: "EmailTester",
                url: "email-tester",
                defaults: new { controller = "Email", action = "SendJobRecap" }
            );

            routes.MapRoute(
                name: "GetTimingFormulaParameters",
                url: "get-timing-formula-params",
                defaults: new { controller = "Services", action = "GetFormulaParameters" }
            );

            routes.MapRoute(
                name: "CancelPurchaseOrder",
                url: "cancel-po",
                defaults: new { controller = "PurchaseOrder", action = "CancelPurchaseOrder" }
            );

            routes.MapRoute(
                name: "ApprovePurchaseOrder",
                url: "approve-po",
                defaults: new { controller = "PurchaseOrder", action = "ApprovePurchaseOrder" }
            );

            routes.MapRoute(
                name: "DenyPurchaseOrder",
                url: "deny-po",
                defaults: new { controller = "PurchaseOrder", action = "DenyPurchaseOrder" }
            );

            routes.MapRoute(
                name: "AcceptDelivery",
                url: "accept-delivery/{itemId}/{quantityReceived}",
                defaults: new { controller = "PurchaseOrder", action = "AcceptDelivery" }
            );

            routes.MapRoute(
                name: "GetDeliveryStatus",
                url: "get-delivery-status/{rowId}",
                defaults: new { controller = "PurchaseOrder", action = "GetDeliveryStatus", rowId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RemovePOItem",
                url: "remove-po-item",
                defaults: new { controller = "PurchaseOrder", action = "RemoveRow" }
            );

            routes.MapRoute(
                name: "NewPORow",
                url: "new-po-row/{poId}",
                defaults: new { controller = "PurchaseOrder", action = "NewRow", poId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetVendorAddressInfo",
                url: "get-vendor-address-info/{id}",
                defaults: new { controller = "Vendors", action = "GetAddressInfo", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "PurchaseOrders",
                url: "PurchaseOrders",
                defaults: new { controller = "PurchaseOrder", action = "Index" }
            );

            routes.MapRoute(
                name: "PurchaseOrderApprovalSubmission",
                url: "submit-po-for-approval",
                defaults: new { controller = "PurchaseOrder", action = "SubmitForApproval" }
            );

            routes.MapRoute(
                name: "PurchaseOrderCreate",
                url: "PurchaseOrders/Create",
                defaults: new { controller = "PurchaseOrder", action = "Create" }
            );

            routes.MapRoute(
                name: "PurchaseOrderEdit",
                url: "PurchaseOrders/Edit/{id}",
                defaults: new { controller = "PurchaseOrder", action = "Edit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AddCuttingServices",
                url: "add-cutting-services",
                defaults: new { controller = "Services", action = "AddCuttingServices" }
            );

            routes.MapRoute(
                name: "PullExternalRecordsBHR",
                url: "ext-bhr-pull",
                defaults: new { controller = "Data", action = "ExternalBHRPull" }
            );

            routes.MapRoute(
                name: "BambooHRTest",
                url: "test-bamboo",
                defaults: new { controller = "Data", action = "GetBambooHRTimeEntries" }
            );

            routes.MapRoute(
                name: "UpdateJobAssignments",
                url: "update-job-assignments",
                defaults: new { controller = "Job", action = "UpdateJobAssignments" }
            );

            routes.MapRoute(
                name: "GetAllServices",
                url: "get-all-services",
                defaults: new { controller = "Services", action = "GetServices" }
            );

            routes.MapRoute(
                name: "UpdateFormulaConfig",
                url: "update-formula-config",
                defaults: new { controller = "Services", action = "UpdateFormulaConfig" }
            );

            routes.MapRoute(
                name: "DeleteShippingAddress",
                url: "delete-shipping-address",
                defaults: new { controller = "Customers", action = "DeleteShippingAddress" }
            );

            routes.MapRoute(
                name: "DeleteQuote",
                url: "delete-quote/{quoteId}",
                defaults: new { controller = "Job", action = "DeleteQuote", quoteId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetUserClockActivity",
                url: "get-clock-activity/{id}",
                defaults: new { controller = "User", action = "GetClockActivity", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SetJobAsShipped",
                url: "set-job-shipped/{id}",
                defaults: new { controller = "Job", action = "SetAsShipped", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SetJobAsComplete",
                url: "set-job-complete/{id}",
                defaults: new { controller = "Job", action = "SetAsComplete", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveJobTableItemNotes",
                url: "save-item-notes-from-table",
                defaults: new { controller = "Job", action = "SaveItemNotesFromTable" }
            );

            routes.MapRoute(
                name: "GetDeliveryTicket",
                url: "delivery-ticket/{job}",
                defaults: new { controller = "Job", action = "GetDeliveryTicket2", job = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "CreateQuotePrintServices",
                url: "m-quote-print-services",
                defaults: new { controller = "Dev", action = "Migration_CreateQuotePrintServices" }
            );

            routes.MapRoute(
                name: "MigrationJobPricingRecords",
                url: "m-job-pricing-records",
                defaults: new { controller = "Dev", action = "Migration_CreateJobPricingRecords" }
            );
            routes.MapRoute(
                name: "MigrationQuotePricingRecords",
                url: "m-quote-pricing-records",
                defaults: new { controller = "Dev", action = "Migration_CreateQuotePricingRecords" }
            );

            routes.MapRoute(
                name: "CreateMaterialDynamic",
                url: "material/dynamic-create",
                defaults: new { controller = "Material", action = "DynamicMaterialAddition" }
            );

            routes.MapRoute(
                name: "CreateVendorDynamic",
                url: "vendor/dynamic-create",
                defaults: new { controller = "Vendors", action = "DynamicVendorCreation" }
            );

            routes.MapRoute(
                name: "ReorderInventoryReport",
                url: "reorder-inventory-report",
                defaults: new { controller = "Reports", action = "Report_GetReorderInventoryReport2" }
            );

            routes.MapRoute(
                name: "GeneralInventoryReport",
                url: "gen-inventory-report",
                defaults: new { controller = "Reports", action = "Report_GetGeneralInventoryReport" }
            );

            routes.MapRoute(
                name: "EstimatedVsActualReport",
                url: "eva-report/{jobList}",
                defaults: new { controller = "Reports", action = "Report_GetEstimatedVsActualTimeReport", jobList = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TimeReport",
                url: "time-report/{id}/{Start}/{End}",
                defaults: new { controller = "Reports", action = "GetTimeReport", id = UrlParameter.Optional, Start = UrlParameter.Optional, End = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ReprintReport",
                url: "reprint-report/{Start}/{End}",
                defaults: new { controller = "Reports", action = "Report_GetReprintReport", Start = UrlParameter.Optional, End = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "WIPReport",
                url: "wip-report/{asOfDate}",
                defaults: new { controller = "Reports", action = "GetWIPReport", asOfDate = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "MaterialAssignments",
                url: "material-assignments",
                defaults: new { controller = "Reports", action = "MaterialAssignments" }
            );

            routes.MapRoute(
                name: "CompletedJobsAccuracy",
                url: "completed-jobs-accuracy",
                defaults: new { controller = "Reports", action = "CompletedJobsAccuracy" }
            );

            routes.MapRoute(
                name: "CompletedJobsAccuracyData",
                url: "completed-jobs-accuracy/data",
                defaults: new { controller = "Reports", action = "GetCompletedJobsAccuracy" }
            );

            routes.MapRoute(
                name: "PressServiceUtilization",
                url: "press-service-utilization",
                defaults: new { controller = "Reports", action = "PressServiceUtilization" }
            );

            routes.MapRoute(
                name: "PressServiceUtilizationData",
                url: "press-service-utilization/data",
                defaults: new { controller = "Reports", action = "GetPressServiceUtilization" }
            );

            routes.MapRoute(
                name: "GetAllJobs",
                url: "get-all-jobs",
                defaults: new { controller = "Reports", action = "GetAllJobs" }
            );

            routes.MapRoute(
                name: "AdjustStock",
                url: "adjust-stock",
                defaults: new { controller = "Inventory", action = "HttpAdjustStock" }
            );

            routes.MapRoute(
                name: "AllocateStock",
                url: "allocate-stock",
                defaults: new { controller = "Inventory", action = "HttpAllocateStock" }
            );

            routes.MapRoute(
                name: "RemoveStock",
                url: "remove-stock",
                defaults: new { controller = "Inventory", action = "HttpRemoveStock" }
            );

            routes.MapRoute(
                name: "OrderStock",
                url: "new-material-order",
                defaults: new { controller = "Inventory", action = "HttpAddOrder" }
            );

            routes.MapRoute(
                name: "DeallocateStock",
                url: "deallocate-stock",
                defaults: new { controller = "Inventory", action = "HttpDeallocateStock" }
            );

            routes.MapRoute(
                name: "LoadPendingOrders",
                url: "get-pending-orders/{matId}",
                defaults: new { controller = "Inventory", action = "GetPendingOrders", matId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetMaterialInventory",
                url: "get-material-inventory",
                defaults: new { controller = "Inventory", action = "GetMaterialInventory" }
            );

            routes.MapRoute(
                name: "GenerateCustomerQuote3",
                url: "generate-customer-quote3/{quoteId}",
                defaults: new { controller = "Job", action = "CustomerQuote3", quoteId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GenerateCustomerQuote4",
                url: "generate-customer-quote4/{quoteId}/{showTotal}",
                defaults: new { controller = "Job", action = "CustomerQuote4", quoteId = UrlParameter.Optional, showTotal = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SubitemLink",
                url: "subitem-link",
                defaults: new { controller = "Job", action = "SubitemLink" }
            );

            routes.MapRoute(
                name: "SubitemUnlink",
                url: "subitem-unlink",
                defaults: new { controller = "Job", action = "SubitemUnlink" }
            );

            routes.MapRoute(
                name: "MarkupOverride",
                url: "override-specific-markup",
                defaults: new { controller = "Job", action = "SpecificMarkupOverride" }
            );

            routes.MapRoute(
                name: "OverrideItemPrice",
                url: "override-item-price",
                defaults: new { controller = "Job", action = "OverrideItemPrice" }
            );

            routes.MapRoute(
                name: "RevertItemPrice",
                url: "revert-item-price",
                defaults: new { controller = "Job", action = "RevertItemPrice" }
            );

            routes.MapRoute(
                name: "ResetServiceMarkup",
                url: "reset-service-markup",
                defaults: new { controller = "Job", action = "ResetServiceMarkup" }
            );


            routes.MapRoute(
                name: "FreightAddressChange",
                url: "freight-address-change",
                defaults: new { controller = "Job", action = "FreightAddressChange" }
            );

            routes.MapRoute(
                name: "ShippingAddressChange",
                url: "shipping-address-change",
                defaults: new { controller = "Job", action = "ShippingAddressChange" }
            );

            routes.MapRoute(
                name: "BlindShippingAddressChange",
                url: "blind-shipping-address-change",
                defaults: new { controller = "Job", action = "BlindShippingAddressChange" }
            );

            routes.MapRoute(
                name: "GetBlindShippingAddresses",
                url: "get-blind-shipping-addresses/{clientId}/{recordId}/{recordtype}",
                defaults: new { controller = "Customers", action = "GetBlindShippingAddresses", clientId = UrlParameter.Optional, recordId = UrlParameter.Optional, recordtype = UrlParameter.Optional }
            );

            /* get-shipping-addresses2 is meant for the shipping screen */
            routes.MapRoute(
                name: "GetShippingAddresses2",
                url: "get-shipping-addresses2/{job}",
                defaults: new { controller = "Shipping", action = "GetShippingAddressesForJob", job = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetShippingAddresses",
                url: "get-shipping-addresses/{clientId}/{recordId}/{recordtype}",
                defaults: new { controller = "Customers", action = "GetShippingAddresses", clientId = UrlParameter.Optional, recordId = UrlParameter.Optional, recordtype = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetShippingAddress",
                url: "get-shipping-address/{id}",
                defaults: new { controller = "Customers", action = "GetShippingAddress", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "UpdateShippingAddress",
                url: "update-shipping-address",
                defaults: new { controller = "Customers", action = "UpdateShippingAddress" }
            );

            routes.MapRoute(
                name: "GetFreightAddresses",
                url: "get-freight-addresses/{clientId}/{recordId}/{recordtype}",
                defaults: new { controller = "Customers", action = "GetFreightAddresses", clientId = UrlParameter.Optional, recordId = UrlParameter.Optional, recordtype = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "NewFreightAddressForClient",
                url: "new-freight-address-for-client",
                defaults: new { controller = "Customers", action = "NewFreightShippingAddress" }
            );

            routes.MapRoute(
                name: "NewAddressForClient",
                url: "new-address-for-client",
                defaults: new { controller = "Customers", action = "NewShippingAddress" }
            );

            routes.MapRoute(
                name: "",
                url: "ajax-remove-service-entry/{id}/{itemtype}",
                defaults: new { controller = "Job", action = "RemoveServiceItemEntry", id = UrlParameter.Optional, itemtype = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RemoveFinishingMaterialService",
                url: "ajax-remove-finishing-material/{id}/{itemtype}",
                defaults: new { controller = "Job", action = "RemoveFinishingMaterialService", id = UrlParameter.Optional, itemtype = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "DeleteItemServices",
                url: "delete-services/{recordtype}/{id}",
                defaults: new { controller = "Job", action = "DeleteItemServices", recordtype = UrlParameter.Optional, id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "NewContactForClient",
                url: "new-contact-for-client",
                defaults: new { controller = "Customers", action = "NewContact" }
            );
            routes.MapRoute(
                name: "RemoveContactFromClient",
                url: "remove-contact-from-client",
                defaults: new { controller = "Customers", action = "RemoveContact" }
            );

            routes.MapRoute(
                name: "FinishingMaterialsBundling",
                url: "fm-bundling",
                defaults: new { controller = "Formula", action = "FinishingMaterial_BundlingFormula" }
            );

            routes.MapRoute(
                name: "FinishingMaterialByQuantity",
                url: "fm-by-quantity",
                defaults: new { controller = "Formula", action = "FinishingMaterial_ByQuantityFormula" }
            );

            routes.MapRoute(
                name: "FinishingMaterialByLength",
                url: "fm-by-length",
                defaults: new { controller = "Formula", action = "FinishingMaterial_ByLengthFormula" }
            );

            routes.MapRoute(
                name: "GetFinishingMaterialConfig",
                url: "get-finishing-material-configs/{itemType}/{id}",
                defaults: new { controller = "Job", action = "GetFinishingMaterialConfigs", itemType = UrlParameter.Optional, id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "UpdateFinishingConfigs",
                url: "update-finishing-config",
                defaults: new { controller = "Job", action = "UpdateFinishingConfig" }
            );

            routes.MapRoute(
                name: "CopyItemRow",
                url: "copy-item/{id}/{itemType}",
                defaults: new { controller = "Job", action = "CopyItem" }
            );

            routes.MapRoute(
                name: "MaterialChangeReset",
                url: "reset-material-values-on-item",
                defaults: new { controller = "Job", action = "UpdateMaterialReset" }
            );

            routes.MapRoute(
                name: "NewStockForPress",
                url: "new-stock-for-press",
                defaults: new { controller = "Press", action = "CreatePressStockConfig" }
            );

            routes.MapRoute(
                name: "ItemServiceUpdate",
                url: "item-service-update",
                defaults: new { controller = "Job", action = "ServiceRowHandler" }
            );

            routes.MapRoute(
                name: "SetCuttingConfigurations",
                url: "set-cutting-configurations",
                defaults: new { controller = "Job", action = "SetCuttingConfigurations" }
            );

            routes.MapRoute(
                name: "GetCuttingServiceIds",
                url: "get-cs-service-ids",
                defaults: new { controller = "Job", action = "GetCuttingServiceServiceIds" }
            );

            routes.MapRoute(
                name: "GetCuttingServiceConfig",
                url: "get-cutting-service-configuration",
                defaults: new { controller = "Services", action = "GetCuttingServiceConfiguration" }
            );

            routes.MapRoute(
                name: "GetCuttingServices",
                url: "get-cutting-services",
                defaults: new { controller = "Services", action = "GetCuttingServices" }
            );

            routes.MapRoute(
                name: "MaterialPriceOverride",
                url: "mat-price-override",
                defaults: new { controller = "Material", action = "PriceOverride" }
            );

            routes.MapRoute(
                name: "LoadStockConfig",
                url: "load-stock-config/{stockTypeId}/{pressId}",
                defaults: new { controller = "Press", action = "LoadPressStockConfig", stockTypeId = UrlParameter.Optional, pressId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "UserStatus",
                url: "user-status",
                defaults: new { controller = "User", action = "GetUserStatus" }
            );

            routes.MapRoute(
                name: "GetDoc",
                url: "get-document/{id}/{itemType}",
                defaults: new { controller = "Job", action = "GetDocument", id = UrlParameter.Optional, itemType = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AddDoc",
                url: "AddDocument",
                defaults: new { controller = "Job", action = "AddDocument" }
            );

            routes.MapRoute(
                name: "GetDocs",
                url: "GetDocuments/{itemType}/{id}/{isOutsourced}",
                defaults: new { controller = "Job", action = "GetDocuments", itemType = UrlParameter.Optional, id = UrlParameter.Optional, isOutsourced = UrlParameter.Optional }
            );

            // Proof routes
            routes.MapRoute(
                name: "AddProof",
                url: "Job/AddProof",
                defaults: new { controller = "Job", action = "AddProof" }
            );

            routes.MapRoute(
                name: "GetProofs",
                url: "Job/GetProofs/{itemId}",
                defaults: new { controller = "Job", action = "GetProofs", itemId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "DeleteProof",
                url: "Job/DeleteProof",
                defaults: new { controller = "Job", action = "DeleteProof" }
            );

            routes.MapRoute(
                name: "DownloadProof",
                url: "Job/DownloadProof/{proofId}",
                defaults: new { controller = "Job", action = "DownloadProof", proofId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetProofThumbnail",
                url: "Job/GetProofThumbnail/{proofId}",
                defaults: new { controller = "Job", action = "GetProofThumbnail", proofId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetProofAccessLink",
                url: "Job/GetProofAccessLink/{jobId}",
                defaults: new { controller = "Job", action = "GetProofAccessLink", jobId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "JobTicket",
                url: "GetJobTicket/{job}",
                defaults: new { controller = "Job", action = "GetJobTicket", job = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "QuoteTicket",
                url: "GetQuoteTicket/{quote}",
                defaults: new { controller = "Job", action = "GetJobTicket", quote = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AddUserToService",
                url: "add-operator-to-job",
                defaults: new { controller = "Services", action = "AddUserToJobService" }
            );

            routes.MapRoute(
                name: "SaveClient",
                url: "client/save",
                defaults: new { controller = "Customers", action = "SaveCustomer" }
            );

            routes.MapRoute(
                name: "NewClient",
                url: "client/new",
                defaults: new { controller = "Customers", action = "NewCustomer" }
            );

            routes.MapRoute(
                name: "EditClient",
                url: "client/edit/{id}",
                defaults: new { controller = "Customers", action = "EditCustomer" }
            );

            routes.MapRoute(
                name: "AdminLoadCategory",
                url: "admin/load-cat",
                defaults: new { controller = "Category", action = "LoadCategory" }
            );

            routes.MapRoute(
                name: "DeleteIncompleteRecord",
                url: "delete-incomplete-record",
                defaults: new { controller = "Job", action = "DeleteIncomplete" }
            );

            routes.MapRoute(
                name: "SaveItemNotes",
                url: "save-item-notes",
                defaults: new { controller = "Job", action = "SaveItemNotes" }
            );

            routes.MapRoute(
                name: "GetItemNotes",
                url: "get-item-notes/{id}/{itemType}/{isTemp}",
                defaults: new { controller = "Job", action = "GetItemNotes", id = UrlParameter.Optional, itemType = UrlParameter.Optional, isTemp = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ItemLoadSpeeds",
                url: "item-load-speeds/{id}/{itemId}",
                defaults: new { controller = "Press", action = "ItemLoadSpeeds", id = UrlParameter.Optional, itemId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveSpeedArrangement",
                url: "save-speed-arrangement",
                defaults: new { controller = "Press", action = "SaveSpeedArrangement" }
            );

            routes.MapRoute(
                name: "SavePressSpeed",
                url: "save-press-speed",
                defaults: new { controller = "Press", action = "SavePressSpeed" }
            );

            routes.MapRoute(
                name: "GetPressSpeed",
                url: "get-press-speed/{pressSpeedId}",
                defaults: new { controller = "Press", action = "GetPressSpeedForEdit", pressSpeedId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetPressSpeeds",
                url: "get-press-speeds/{press}",
                defaults: new { controller = "Press", action = "GetPressSpeeds", press = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AjaxDecimalHours",
                url: "formatted-time-to-decimal",
                defaults: new { controller = "Data", action = "AjaxFormattedTimeToDecimal", decimalHours = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AjaxFormattedTime",
                url: "decimal-time-to-formatted",
                defaults: new { controller = "Data", action = "AjaxDecimalHoursToFormatted", decimalHours = UrlParameter.Optional }
            );

            /* FORMULA SPECIFIC ROUTES */
            routes.MapRoute(
                name: "DieCuttingFormula",
                url: "formula/die-cutting",
                defaults: new { controller = "Formula", action = "DieCuttingFormula" }
            );
            routes.MapRoute(
                name: "ThreeHolePunchFormula",
                url: "formula/three-hole-punch",
                defaults: new { controller = "Formula", action = "ThreeHolePunchFormula" }
            );
            routes.MapRoute(
                name: "VariableHolePunchFormula",
                url: "formula/var-hole-punch",
                defaults: new { controller = "Formula", action = "VariableHolePunchFormula" }
            );
            routes.MapRoute(
                name: "FoldingTimeFormula",
                url: "formula/folding-time",
                defaults: new { controller = "Formula", action = "FoldingTimeFormula" }
            );
            routes.MapRoute(
                name: "ScoringFormula",
                url: "formula/scoring-formula",
                defaults: new { controller = "Formula", action = "ScoringFormula" }
            );
            routes.MapRoute(
                name: "PolybaggingFormula",
                url: "formula/polybagging-formula",
                defaults: new { controller = "Formula", action = "PolybaggingFormula" }
            );
            routes.MapRoute(
                name: "SkrinkWrappingFormula",
                url: "formula/shrinkwrapping-formula",
                defaults: new { controller = "Formula", action = "ShrinkWrappingFormula" }
            );
            routes.MapRoute(
                name: "BulkPackingFormula",
                url: "formula/bulk-packing",
                defaults: new { controller = "Formula", action = "BulkPackingFormula" }
            );
            routes.MapRoute(
                name: "KitPackingFormula",
                url: "formula/kit-packing",
                defaults: new { controller = "Formula", action = "KitPackingFormula" }
            );
            routes.MapRoute(
                name: "LinearCuttingFormula",
                url: "formula/linear-cutting",
                defaults: new { controller = "Formula", action = "LinearCuttingFormula" }
            );
            routes.MapRoute(
                name: "ZundCuttingFormula",
                url: "formula/zund-cutting",
                defaults: new { controller = "Formula", action = "ZundCuttingFormula" }
            );
            /* END FORMULA SPECIFIC ROUTES */
            routes.MapRoute(
                name: "CheckConstraintValidity",
                url: "check-constraint",
                defaults: new { controller = "Press", action = "ConstraintIsValid" }
            );

            routes.MapRoute(
                name: "GetMaterialModel",
                url: "get-material-model",
                defaults: new { controller = "Data", action = "GetMaterialModel" }
            );

            routes.MapRoute(
                name: "GetPressModel",
                url: "get-press-model",
                defaults: new { controller = "Data", action = "GetPressModel" }
            );

            routes.MapRoute(
                name: "GetFinishingMaterialsModel",
                url: "get-finishing-materials-model",
                defaults: new { controller = "Data", action = "GetFinishingMaterialsModel" }
            );

            routes.MapRoute(
                name: "CheckUser",
                url: "user-is-valid/{userId}",
                defaults: new { controller = "User", action = "IsUserValid", userId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Logout",
                url: "logout",
                defaults: new { controller = "User", action = "Logout" }
            );

            routes.MapRoute(
                name: "OperatorLogin",
                url: "login",
                defaults: new { controller = "User", action = "Login" }
            );

            routes.MapRoute(
                name: "ConstraintTypeCheck",
                url: "ConstraintTypeCheck/{constraintTypeId}",
                defaults: new { controller = "Press", action = "ConstraintTypeCheck", constraintTypeId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Constraints",
                url: "constraints/{press}",
                defaults: new { controller = "Press", action = "LoadConstraints", press = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetConstraint",
                url: "constraint/get/{id}/{ReturnHtmlString}",
                defaults: new { controller = "Press", action = "GetConstraintForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "NewConstraint",
                url: "constraint/new",
                defaults: new { controller = "Press", action = "NewConstraint" }
            );

            routes.MapRoute(
                name: "SaveConstraint",
                url: "constraint/save",
                defaults: new { controller = "Press", action = "SaveConstraint" }
            );

            routes.MapRoute(
                name: "QuoteServiceArrangement",
                url: "service-arrange/save",
                defaults: new { controller = "Job", action = "SaveServiceArrangement" }
            );

            routes.MapRoute(
                name: "DefaultPressServices",
                url: "Press/GetDefaultServices/{pressId}/{itemId}/{speedId}/{itemtype}",
                defaults: new { controller = "Press", action = "GetDefaultServices", pressId = UrlParameter.Optional, itemId = UrlParameter.Optional, itemtype = UrlParameter.Optional, speedId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AltRunCalcs",
                url: "run-calcs-alt",
                defaults: new { controller = "Data", action = "TestCalcs" }
            );

            routes.MapRoute(
                name: "CreateJobFromQuote",
                url: "Job/CreateJobFromQuote",
                defaults: new { controller = "Job", action = "CreateJobFromQuote" }
            );

            routes.MapRoute(
                name: "CreateQuoteFromJob",
                url: "Job/CreateQuoteFromJob",
                defaults: new { controller = "Job", action = "CreateQuoteFromJob" }
            );

            routes.MapRoute(
                name: "SaveUser",
                url: "user/save",
                defaults: new { controller = "User", action = "Save" }
            );

            routes.MapRoute(
                name: "NewUser",
                url: "user/new",
                defaults: new { controller = "User", action = "NewUser" }
            );

            routes.MapRoute(
                name: "EditUser",
                url: "user/edit/{id}",
                defaults: new { controller = "User", action = "EditUser", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveQuoteItemServiceOverride",
                url: "quote-service-item/save",
                defaults: new { controller = "Services", action = "SaveQuoteItemServiceOverride" }
            );

            routes.MapRoute(
                name: "EditQuoteItemService",
                url: "QuoteItemService/Edit/{id}",
                defaults: new { controller = "Services", action = "GetQuoteItemServiceForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveJobItemServiceOverride",
                url: "job-service-item/save",
                defaults: new { controller = "Services", action = "SaveJobItemServiceOverride" }
            );

            routes.MapRoute(
                name: "EditJobItemService",
                url: "JobItemService/Edit/{id}",
                defaults: new { controller = "Services", action = "GetJobItemServiceForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveConfig",
                url: "save/item-config/{itemType}",
                defaults: new { controller = "Job", action = "SaveItemConfig", itemType = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ValidateJobNumber",
                url: "check/{j}/{id}/{jType}",
                defaults: new { controller = "Data", action = "JobNumberExists", j = UrlParameter.Optional, jType = UrlParameter.Optional, id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AutocompleteItem",
                url: "AutocompleteItem",
                defaults: new { controller = "Data", action = "AutocompleteItem" }
            );

            routes.MapRoute(
                name: "AutocompleteMaterial",
                url: "AutocompleteMaterial",
                defaults: new { controller = "Data", action = "AutocompleteMaterial" }
            );

            routes.MapRoute(
                name: "AutocompleteClient",
                url: "AutocompleteClient",
                defaults: new { controller = "Data", action = "AutocompleteClient" }
            );

            routes.MapRoute(
                name: "AutocompleteJobNumber",
                url: "AutocompleteJobNumber",
                defaults: new { controller = "Data", action = "AutocompleteJobNumber" }
            );

            routes.MapRoute(
                name: "AutocompleteJobsReadyToShip",
                url: "AutocompleteJobsReadyToShip",
                defaults: new { controller = "Data", action = "AutocompleteJobsReadyToShip" }
            );

            routes.MapRoute(
                name: "AutocompleteContact",
                url: "AutocompleteContact",
                defaults: new { controller = "Data", action = "AutocompleteContact" }
            );

            routes.MapRoute(
                name: "AutocompleteActiveJobNumber",
                url: "AutocompleteActiveJobNumber",
                defaults: new { controller = "Data", action = "AutocompleteActiveJobNumber" }
            );

            routes.MapRoute(
                name: "AutocompleteReadyToBillJobNumber",
                url: "AutocompleteReadyToBillJobNumber",
                defaults: new { controller = "Data", action = "AutocompleteReadyToBillJobNumber" }
            );

            routes.MapRoute(
                name: "AutocompleteQuoteNumber",
                url: "AutocompleteQuoteNumber",
                defaults: new { controller = "Data", action = "AutocompleteQuoteNumber" }
            );

            routes.MapRoute(
                name: "AutocompleteVendor",
                url: "AutocompleteVendor",
                defaults: new { controller = "Data", action = "AutocompleteVendor" }
            );

            routes.MapRoute(
                name: "NavbarDuoAutocomplete",
                url: "navbar-d-autocomplete",
                defaults: new { controller = "Data", action = "AutocompleteJobQuote" }
            );

            routes.MapRoute(
                name: "DuplicateQuote",
                url: "quote/duplicate",
                defaults: new { controller = "Job", action = "DuplicateQuote" }
            );

            routes.MapRoute(
                name: "DuplicateEstimate",
                url: "estimate/duplicate/{id}",
                defaults: new { controller = "Job", action = "DuplicateEstimate", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "DuplicateJob",
                url: "job/duplicate/{id}",
                defaults: new { controller = "Job", action = "DuplicateJob", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveService",
                url: "services/save",
                defaults: new { controller = "Services", action = "SaveService" }
            );

            routes.MapRoute(
                name: "SaveMaterial",
                url: "material/save",
                defaults: new { controller = "Material", action = "SaveMaterial" }
            );

            routes.MapRoute(
                name: "SavePress",
                url: "press/save",
                defaults: new { controller = "Press", action = "SavePress" }
            );

            routes.MapRoute(
                name: "EditServiceCat",
                url: "servicecat/edit/{id}",
                defaults: new { controller = "Category", action = "GetServiceCategoryForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SaveServiceCat",
                url: "servicecat/save",
                defaults: new { controller = "Category", action = "SaveServiceCategory" }
            );

            routes.MapRoute(
                name: "NewServiceCat",
                url: "servicecat/new",
                defaults: new { controller = "Category", action = "NewServiceCategory" }
            );

            routes.MapRoute(
                name: "SaveColors",
                url: "color/save",
                defaults: new { controller = "Color", action = "SaveColorProfile" }
            );

            routes.MapRoute(
                name: "NewPress",
                url: "press/new",
                defaults: new { controller = "Press", action = "NewPress" }
            );

            routes.MapRoute(
                name: "NewMaterial",
                url: "material/new",
                defaults: new { controller = "Material", action = "NewMaterial" }
            );

            routes.MapRoute(
                name: "NewPlate",
                url: "plate/new",
                defaults: new { controller = "Plate", action = "NewPlate" }
            );

            routes.MapRoute(
                name: "EditPlate",
                url: "plate/edit/{id}",
                defaults: new { controller = "Plate", action = "GetPlateForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "SavePlate",
                url: "plate/save",
                defaults: new { controller = "Plate", action = "SavePlate" }
            );

            routes.MapRoute(
                name: "NewService",
                url: "service/new",
                defaults: new { controller = "Services", action = "NewService" }
            );

            routes.MapRoute(
                name: "NewColors",
                url: "color/new",
                defaults: new { controller = "Color", action = "NewColorProfile" }
            );

            routes.MapRoute(
                name: "EditService",
                url: "service/edit/{id}",
                defaults: new { controller = "Services", action = "GetServiceForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditMaterial",
                url: "material/edit/{id}",
                defaults: new { controller = "Material", action = "GetMaterialForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditPress",
                url: "press/edit/{id}",
                defaults: new { controller = "Press", action = "GetPressForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditColors",
                url: "color/edit/{id}",
                defaults: new { controller = "Color", action = "GetColorProfileForEdit", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AddOutsourcedItem",
                url: "add-outsourced-item/{itemType}/{masterRecordId}",
                defaults: new { controller = "Job", action = "NewOutsourcedItemRow", itemType = UrlParameter.Optional, masterRecordId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "AddItem",
                url: "add-item/{itemType}/{masterRecordId}",
                defaults: new { controller = "Job", action = "NewItemRow", itemType = UrlParameter.Optional, masterRecordId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RemoveItem",
                url: "remove-item/{itemType}/{itemId}/{isOutsource}",
                defaults: new { controller = "Job", action = "RemoveItemRow", itemType = UrlParameter.Optional, itemId = UrlParameter.Optional, isOutsource = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetServiceSet",
                url: "services/{serviceType}",
                defaults: new { controller = "Services", action = "GetServiceSet", serviceType = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "GetGroupedServices",
                url: "Services/Get/All",
                defaults: new { controller = "Services", action = "GetDefaultServiceSelect" }
            );

            routes.MapRoute(
                name: "GetAllMaterials",
                url: "Materials/Get/All",
                defaults: new { controller = "Material", action = "GetAllMaterials" }
            );

            routes.MapRoute(
                name: "GetQuotes",
                url: "Quotes/Get/All",
                defaults: new { controller = "Job", action = "GetAllQuotes" }
            );

            routes.MapRoute(
                name: "GetEstimates",
                url: "Estimates/Get/All",
                defaults: new { controller = "Job", action = "GetAllEstimates" }
            );

            routes.MapRoute(
                name: "GetJobs",
                url: "Jobs/Get/All",
                defaults: new { controller = "Job", action = "GetAllJobs" }
            );

            routes.MapRoute(
                name: "CreateJob",
                url: "Job/Create",
                defaults: new { controller = "Job", action = "CreateJob" }
            );

            routes.MapRoute(
                name: "CreateQuote",
                url: "Quote/Create",
                defaults: new { controller = "Job", action = "CreateQuote" }
            );

            routes.MapRoute(
                name: "CreateEstimate",
                url: "Estimate/Create",
                defaults: new { controller = "Job", action = "CreateEstimate" }
            );

            routes.MapRoute(
                name: "ViewJob",
                url: "Job/View/{id}",
                defaults: new { controller = "Job", action = "ViewJob", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ViewQuote",
                url: "Quote/View/{id}",
                defaults: new { controller = "Job", action = "ViewQuote", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditJobSingle",
                url: "Job/Edit/{jobId}",
                defaults: new { controller = "Job", action = "EditJob", jobId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditQuoteSingle",
                url: "Quote/Edit/{quoteId}",
                defaults: new { controller = "Job", action = "EditQuote", quoteId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "EditEstimateSingle",
                url: "Estimate/Edit/{estimateId}",
                defaults: new { controller = "Job", action = "EditEstimate", estimateId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ListJobs",
                url: "Job/Edit",
                defaults: new { controller = "Job", action = "ListJobs" }
            );

            routes.MapRoute(
                name: "ListQuotes",
                url: "Quote/Edit",
                defaults: new { controller = "Job", action = "ListQuotes" }
            );

            routes.MapRoute(
                name: "ListEstimates",
                url: "Estimate/Edit",
                defaults: new { controller = "Job", action = "ListEstimates" }
            );

            routes.MapRoute(
                name: "UpdateJob",
                url: "Job/Update",
                defaults: new { controller = "Job", action = "UpdateJob" }
            );

            routes.MapRoute(
                name: "UpdateQuote",
                url: "Quote/Update",
                defaults: new { controller = "Job", action = "UpdateQuote" }
            );

            routes.MapRoute(
                name: "UpdateEstimate",
                url: "Estimate/Update",
                defaults: new { controller = "Job", action = "UpdateEstimate" }
            );

            routes.MapRoute(
                name: "JobDetails",
                url: "Job/Details/{jobId}",
                defaults: new { controller = "Job", action = "Details", jobId = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "UpdateJobStatus",
                url: "UpdateJobStatus",
                defaults: new { controller = "Job", action = "UpdateJobStatus" }
            );

            routes.MapRoute(
                name: "apiRefresh",
                url: "Refresh/{LastUpdate}",
                defaults: new { controller = "Data", action = "Refresh", LastUpdate = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "JobList",
                url: "Jobs",
                defaults: new { controller = "Job", action = "Index" }
            );

            routes.MapRoute(
                name: "TimeTracking",
                url: "TimeTracking",
                defaults: new { controller = "TimeTracking", action = "Index" }
            );

            routes.MapRoute(
                name: "Settings",
                url: "Settings",
                defaults: new { controller = "Settings", action = "Index" }
            );

            routes.MapRoute(
                name: "Administration",
                url: "Admin",
                defaults: new { controller = "Administration", action = "Index" }
            );

            routes.MapRoute(
                name: "AdminNav",
                url: "AdminNav/{toLoad}",
                defaults: new { controller = "Administration", action = "AdminNav", toLoad = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Inventory",
                url: "Inventory",
                defaults: new { controller = "Inventory", action = "Index" }
            );

            routes.MapRoute(
                name: "Services",
                url: "Services",
                defaults: new { controller = "Services", action = "Index" }
            );

            routes.MapRoute(
                name: "Customers",
                url: "Customers",
                defaults: new { controller = "Customers", action = "Index" }
            );
            routes.MapRoute(
                name: "Vendors",
                url: "Vendors",
                defaults: new { controller = "Vendors", action = "Index" }
            );
            routes.MapRoute(
                name: "AccountsReceivable",
                url: "AR",
                defaults: new { controller = "AccountsReceivables", action = "Index" }
            );
            routes.MapRoute(
                name: "Reports",
                url: "Reports",
                defaults: new { controller = "Reports", action = "Index" }
            );

            routes.MapRoute(
                name: "PrintDashboard",
                url: "PrintDashboard",
                defaults: new { controller = "Home", action = "Index_New" }
            );

            routes.MapRoute(
                name: "McpApiJob",
                url: "mcp-api/job",
                defaults: new { controller = "McpApi", action = "GetJob" }
            );

            routes.MapRoute(
                name: "McpApiSimilarCandidates",
                url: "mcp-api/similar-candidates",
                defaults: new { controller = "McpApi", action = "GetSimilarCandidates" }
            );

            routes.MapRoute(
                name: "McpApiJobsCreatedReport",
                url: "mcp-api/reports/jobs-created",
                defaults: new { controller = "McpApi", action = "GetJobsCreatedReport" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Splash", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}