/// <summary>
/// Selectable partial amount of one stack: [1, maxAmount - 1]. The whole stack stays with
/// the explicit full-stack actions, so stacks below two units have no selectable range.
/// </summary>
public readonly struct QuantitySelectorRange
{
    public const int Min = 1;

    private QuantitySelectorRange(int max)
    {
        Max = max;
    }

    public int Max { get; }
    public bool IsValid => Max >= Min;

    public static bool TryCreate(int maxAmount, out QuantitySelectorRange range)
    {
        range = maxAmount > Min ? new QuantitySelectorRange(maxAmount - 1) : default;
        return range.IsValid;
    }

    public static bool IsAcceptedCharacter(char character) => character >= '0' && character <= '9';

    public bool Contains(int amount) => IsValid && amount >= Min && amount <= Max;

    public int Clamp(int amount) => amount < Min ? Min : amount > Max ? Max : amount;

    /// <summary>Accepts ASCII digits only; signs, separators, whitespace and overflow are rejected.</summary>
    public bool TryParse(string text, out int amount)
    {
        amount = 0;
        if (!IsValid || string.IsNullOrEmpty(text))
        {
            return false;
        }

        long value = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!IsAcceptedCharacter(text[i]))
            {
                return false;
            }

            value = value * 10 + (text[i] - '0');
            if (value > Max)
            {
                return false;
            }
        }

        if (value < Min)
        {
            return false;
        }

        amount = (int)value;
        return true;
    }
}
