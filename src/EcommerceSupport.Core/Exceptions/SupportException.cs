namespace EcommerceSupport.Core.Exceptions;

public class SupportException(string errorCode, string message, Exception? inner = null) : Exception(message, inner)
{
    public string ErrorCode { get; } = errorCode;
}

public class OrderNotFoundException(string orderId) : SupportException("ORDER_NOT_FOUND", $"Order '{orderId}' was not found.")
{
    public string OrderId { get; } = orderId;
}

public class CustomerNotFoundException(string customerId) : SupportException("CUSTOMER_NOT_FOUND", $"Customer '{customerId}' was not found.")
{
    public string CustomerId { get; } = customerId;
}

public class RefundNotEligibleException(string orderId, string reason) : SupportException("REFUND_NOT_ELIGIBLE", $"Order '{orderId}' is not eligible for a refund: {reason}")
{
}

public class WorkflowException(string message, Exception? inner = null) : SupportException("WORKFLOW_ERROR", message, inner)
{
}
