using System.ComponentModel.DataAnnotations;
using Common.Extensions;

namespace Common;

public static class ModelUtils
{
    public static void ValidateModel(object model)
    {
        ValidationContext context = new(model, null, null);
        List<ValidationResult> results = [];

        if (!Validator.TryValidateObject(model, context, results, true))
        {
            ValidationResult firstError = results.First();

            throw new ValidationException($"Paramètre '{firstError.MemberNames.JoinString()}' incorrect : {firstError.ErrorMessage}");
        }
    }
}