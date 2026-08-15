using System;

/// <summary>
/// Auxiliary struct used to emit responses from the network layer to the local orchestrator.
/// Not synchronized across the network natively.
/// </summary>
public readonly struct ShopTransactionResponse
{
    public readonly int ClientSequence;
    public readonly bool IsApproved;
    public readonly ShopTransactionId TransactionId;

    public ShopTransactionResponse(int sequence, bool approved, ShopTransactionId transactionId)
    {
        ClientSequence = sequence;
        IsApproved = approved;
        TransactionId = transactionId;
    }
}
