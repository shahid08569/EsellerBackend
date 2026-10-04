using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.RegisterSeller;

public record RegisterSellerCommand(
    // Personal Details
    string Name,
    string Email,
    string Phone,
    string Password,

    // Shop Details
    string StoreName,
    string StoreUrl,
    string? StoreDescription,
    string Address,
    string City,
    string Country,

    // Verification Documents
    string? DocumentType,
    string? DocumentNumber,
    string? CnicFrontUrl,
    string? CnicBackUrl,
    string? DocumentUrl,

    // Location
    decimal Latitude,
    decimal Longitude,
    string DeviceType) : IRequest<Result<RegisterSellerResponse>>;