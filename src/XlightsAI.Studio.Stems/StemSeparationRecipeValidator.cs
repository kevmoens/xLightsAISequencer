namespace XlightsAI.Studio.Stems;

/// <summary>Validates a <see cref="StemSeparationRecipe"/>'s declared shape before it is ever used to drive inference.</summary>
public static class StemSeparationRecipeValidator
{
    public static void Validate(StemSeparationRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (string.IsNullOrWhiteSpace(recipe.RecipeVersion))
        {
            throw new StemSeparationRecipeInvalidException("RecipeVersion must not be empty.");
        }

        if (recipe.ExpectedSampleRate <= 0)
        {
            throw new StemSeparationRecipeInvalidException(
                $"ExpectedSampleRate must be positive (got {recipe.ExpectedSampleRate}).");
        }

        if (recipe.ExpectedChannels <= 0)
        {
            throw new StemSeparationRecipeInvalidException(
                $"ExpectedChannels must be positive (got {recipe.ExpectedChannels}).");
        }

        if (recipe.StemOrder is null || recipe.StemOrder.Count == 0)
        {
            throw new StemSeparationRecipeInvalidException("StemOrder must declare at least one stem.");
        }

        if (string.IsNullOrWhiteSpace(recipe.InputTensorName))
        {
            throw new StemSeparationRecipeInvalidException("InputTensorName must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(recipe.OutputTensorName))
        {
            throw new StemSeparationRecipeInvalidException("OutputTensorName must not be empty.");
        }

        if (recipe.NormalizationEpsilon <= 0)
        {
            throw new StemSeparationRecipeInvalidException(
                $"NormalizationEpsilon must be positive (got {recipe.NormalizationEpsilon}).");
        }

        if (recipe.ChunkSamples <= 0)
        {
            throw new StemSeparationRecipeInvalidException(
                $"ChunkSamples must be positive (got {recipe.ChunkSamples}).");
        }

        if (recipe.OverlapSamples < 0)
        {
            throw new StemSeparationRecipeInvalidException(
                $"OverlapSamples must not be negative (got {recipe.OverlapSamples}).");
        }

        if (recipe.OverlapSamples >= recipe.ChunkSamples)
        {
            throw new StemSeparationRecipeInvalidException(
                $"OverlapSamples ({recipe.OverlapSamples}) must be strictly less than ChunkSamples ({recipe.ChunkSamples}).");
        }
    }
}
