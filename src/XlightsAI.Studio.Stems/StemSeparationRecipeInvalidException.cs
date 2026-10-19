namespace XlightsAI.Studio.Stems;

/// <summary>Thrown by <see cref="StemSeparationRecipeValidator.Validate"/> when a <see cref="StemSeparationRecipe"/>'s own declared shape is structurally invalid.</summary>
public sealed class StemSeparationRecipeInvalidException : Exception
{
    public StemSeparationRecipeInvalidException(string message) : base(message)
    {
    }
}
