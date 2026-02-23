namespace BrokenProject;

// CS0246: Missing using for JsonConvert (Newtonsoft.Json)
public class DataProcessor
{
    public string Serialize(object data)
    {
        // This will cause CS0246 because Newtonsoft.Json is misspelled in the csproj
        return JsonConvert.SerializeObject(data);
    }

    public void Process(string input)
    {
        // CS0029: Cannot implicitly convert type 'string' to 'int'
        int count = input;

        // CS8600: Nullable warning (treated as error)
        string? maybeNull = null;
        string definitelyNotNull = maybeNull;
    }
}
