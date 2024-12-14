namespace LambdaLayerObjects;
public class Food
{
    public string item_name { get; set; }
    public string party_code { get; set; }
    public string status { get; set; }
    public string username { get; set; }
    public Guid cognito_username { get; set; }
}