using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress
{
    /// <summary>
    /// usage ex: PurchaseOrderLogger.Log($"[OVERRIDE] User {userId} overrode budget restriction for PO {po.PO_OrderId}. Amount: {poTotal:C}, Remaining: {budgetResult.BudgetRemaining:C}");
    /// </summary>
    public static class PurchaseOrderLogger
    {
        private const string Scope = "PO";
        private const string LogPath = @"C:\tmp\po-dev.log";

        static PurchaseOrderLogger()
        {
            MastermindLogger.SetScopedLog(Scope, LogPath);
        }

        public static void Log(string message)
        {
            MastermindLogger.UseScope(Scope);
            MastermindLogger.Log(LogLevel.Info, message);
            MastermindLogger.ClearScope();
        }

        public static void LogError(string message)
        {
            MastermindLogger.UseScope(Scope);
            MastermindLogger.Log(LogLevel.Error, message);
            MastermindLogger.ClearScope();
        }

        public static void LogWarning(string message)
        {
            MastermindLogger.UseScope(Scope);
            MastermindLogger.Log(LogLevel.Warning, message);
            MastermindLogger.ClearScope();
        }

        public static void LogException(Exception ex)
        {
            MastermindLogger.UseScope(Scope);
            MastermindLogger.LogException(ex);
            MastermindLogger.ClearScope();
        }
    }
}