namespace LambdaLayerObjects;
public class Guest
{
    public string username { get; set; }
    public string guest_name { get; set; }
    public string party_code { get; set; }
    public int at_party { get; set; }
    public Guid cognito_username { get; set; }
}