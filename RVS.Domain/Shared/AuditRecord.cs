namespace RVS.Domain.Shared;

public class AuditRecord
{
    public long Id { get; set; }
    public Guid Guid { get; set; }
    public DateTime AccessTime { get; set; }
    public DateTime LogTime { get; set; }
    public string UserId { get; set; }
    public Guid UserGuid { get; set; }
    public string UserEmail { get; set; }
    public string UserIPAddress { get; set; }
    public string CitizenOfCountry { get; set; }
    public string Company { get; set; }
    public string Application { get; set; }
    public string PageVisited { get; set; }
    public string APICall { get; set; }
    public int ClassificationId { get; set; }
    public Guid ClassificationGuid { get; set; }

}
