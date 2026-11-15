namespace Leyeco2Dashboard.Data.Enums
{
    /// <summary>
    /// Where a bill stands in the online payment facility's <c>PostedPaymentsQueue</c>, the hand-off between a payment
    /// the facility recorded as successful and its posting into the collections ledger. The facility writes
    /// <see cref="Queued"/> when a provider confirms the payment (leyeco2-otf's <c>ResolveTransactionCommand</c>); the
    /// poster that drains the queue lives outside the facility's solution and moves a row through the others. The
    /// numbers are its own, as stored.
    /// </summary>
    public enum PostedPaymentQueueStatusEnum
    {
        /// <summary>The poster tried and could not post it; <c>Remarks</c> says why.</summary>
        Failed = -1,

        /// <summary>Waiting for the poster.</summary>
        Queued = 0,

        /// <summary>The poster has picked it up and not yet finished.</summary>
        Posting = 1,

        /// <summary>The poster reports it posted into the ledger.</summary>
        Posted = 2
    }
}
