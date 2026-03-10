namespace RVS.Domain.Shared;

public interface IAuditRepository
{
    Task SaveAuditRecordAsync(AuditRecord auditRecord);
    Task SaveAuditRecordWithDefaultsAsync(AuditRecord auditRecord);
}
