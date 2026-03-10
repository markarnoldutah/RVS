using RVS.Domain.Shared;

namespace RVS.Infra.AzTablesRepository;
public class TablesAuditRepository : IAuditRepository

{
    public TablesAuditRepository()
    {
        // initialize Az Tables
    }

    public Task SaveAuditRecordAsync(AuditRecord auditRecord)
    {
        throw new NotImplementedException();
    }

    public Task SaveAuditRecordWithDefaultsAsync(AuditRecord auditRecord)
    {
        throw new NotImplementedException();
    }
}
