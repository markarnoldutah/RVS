namespace RVMTP.Domain.Shared;

public static class GeneralUtils
{
    /// <summary>
    /// Returns the input string with any non-numeric characters removed.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string StripLetters(string input)
    {
        return new string(input.Where(c => char.IsDigit(c)).ToArray());
    }

    /// <summary>
    /// Returns true if the passed in string can be successfully converted to a long, otherwise returns false
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static bool IsStringNumeric(string input)
    {
        long output = 0;
        bool isNumeric = long.TryParse(input, out output);
        return isNumeric;
    }

}


