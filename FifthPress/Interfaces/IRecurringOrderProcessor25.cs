using System.Threading.Tasks;

namespace FifthPress.Interfaces
{
    /// <summary>
    /// Interface for recurring order processing service
    /// </summary>
    public interface IRecurringOrderProcessor25
    {
        /// <summary>
        /// Process all recurring schedules that are due (NextProcessDate <= tomorrow)
        /// </summary>
        Task ProcessDueRecurringOrdersAsync();

        /// <summary>
        /// Process a specific recurring schedule by ID
        /// </summary>
        /// <param name="recurringId">The recurring schedule ID to process</param>
        Task ProcessRecurringScheduleAsync(int recurringId);

        /// <summary>
        /// Start the background timer for automatic processing
        /// </summary>
        void StartProcessing();

        /// <summary>
        /// Stop the background timer
        /// </summary>
        void StopProcessing();
    }
}
