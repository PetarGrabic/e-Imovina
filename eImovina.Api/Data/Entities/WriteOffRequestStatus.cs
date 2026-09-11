namespace eImovina.Api.Data.Entities;

public class WriteOffRequestStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
