using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Content;

public class Setting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}