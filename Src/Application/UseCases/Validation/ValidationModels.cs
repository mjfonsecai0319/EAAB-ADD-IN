using System.Collections.Generic;

namespace EAABAddIn.Src.Application.UseCases.Validation
{
    public record DatasetInput(string Name, string Path);

    public class ValidationRequest
    {
        public string OutputFolder { get; set; } = string.Empty;
        public List<DatasetInput> Datasets { get; set; } = new();
    }

    public class ValidationResult
    {
        public int TotalWarnings { get; set; } = 0;
        public string ReportFolder { get; set; } = string.Empty;
        public List<string> ReportFiles { get; set; } = new();
    }
}
