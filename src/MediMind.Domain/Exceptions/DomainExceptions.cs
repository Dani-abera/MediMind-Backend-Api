namespace MediMind.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule is violated in the domain layer.
/// Always results in HTTP 400 Bad Request at the API layer.
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>
/// Thrown when a requested resource is not found.
/// Results in HTTP 404 Not Found.
/// </summary>
public class NotFoundException(string entityName, object key)
    : Exception($"{entityName} with key '{key}' was not found.");

/// <summary>
/// Thrown when a user attempts to access a resource they don't own or have access to.
/// Results in HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException(string message = "Access denied.") : Exception(message);

/// <summary>
/// Thrown when tenant isolation is violated.
/// Results in HTTP 403 Forbidden.
/// </summary>
public class TenantIsolationException()
    : Exception("Cross-tenant data access is strictly prohibited.");

/// <summary>
/// Thrown when an authenticated user is not authorized for a resource.
/// Results in HTTP 403 Forbidden.
/// </summary>
public class UnauthorizedException(string message = "Access denied") : Exception(message);

/// <summary>
/// Thrown when an external dependency is temporarily unavailable.
/// Results in HTTP 503 Service Unavailable.
/// </summary>
public class ServiceUnavailableException(string message = "Service temporarily unavailable.") : Exception(message);

/// <summary>
/// Thrown when a payment gateway or payment flow step fails.
/// </summary>
public class PaymentException(string message) : Exception(message);
