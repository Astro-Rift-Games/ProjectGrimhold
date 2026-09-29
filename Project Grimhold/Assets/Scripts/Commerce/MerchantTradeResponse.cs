using System;

/// <summary>
/// State Authority answer to one trade request, addressed by its client request id. An approved
/// response carries the authority-issued ticket; a rejected one carries none.
/// </summary>
public readonly struct MerchantTradeResponse
{
    public Guid RequestId { get; }
    public bool IsApproved => Ticket != null;
    public MerchantTradeTicket Ticket { get; }

    private MerchantTradeResponse(Guid requestId, MerchantTradeTicket ticket)
    {
        RequestId = requestId;
        Ticket = ticket;
    }

    public static MerchantTradeResponse Approved(Guid requestId, MerchantTradeTicket ticket)
    {
        return new MerchantTradeResponse(requestId, ticket ?? throw new ArgumentNullException(nameof(ticket)));
    }

    public static MerchantTradeResponse Rejected(Guid requestId)
    {
        return new MerchantTradeResponse(requestId, null);
    }
}
