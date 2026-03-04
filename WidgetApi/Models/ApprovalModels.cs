namespace WidgetApi.Models;

public class ApprovalItem
{
    public int    Approval     { get; set; }
    public string ApprovalName { get; set; } = string.Empty;
    public int    RoleType     { get; set; }
    public string LastStatus   { get; set; } = string.Empty;
    public int    Request      { get; set; }
    public int    Accepted     { get; set; }
    public int    Rejected     { get; set; }
    public int    Submitted    { get; set; }
}

public class ApprovalSummaryDto
{
    public int                TotalPending  { get; set; }
    public List<ApprovalItem> Approvals     { get; set; } = new();
}
