namespace LambdaLayerObjects;
public class Host
{
    public string username { get; set; }
    public string party_name { get; set; }
    public string party_code { get; set; }
    public int invite_only { get; set; }
    public Guid cognito_username { get; set; }
}