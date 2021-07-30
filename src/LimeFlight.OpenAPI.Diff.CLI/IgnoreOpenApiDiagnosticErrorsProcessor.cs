using System.Collections.Generic;
using LimeFlight.OpenAPI.Diff.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace LimeFlight.OpenAPI.Diff.CLI
{
	public class IgnoreOpenApiDiagnosticErrorsProcessor : OpenApiDiagnosticErrorsProcessor
	{
		private readonly ILogger<IgnoreOpenApiDiagnosticErrorsProcessor> _logger;

		public IgnoreOpenApiDiagnosticErrorsProcessor(ILogger<IgnoreOpenApiDiagnosticErrorsProcessor> logger)
		{
			_logger = logger;
		}

		public override void ProcessingErrors(string filePath, ICollection<OpenApiError> openApiErrors)
		{
			foreach (var apiError in openApiErrors)
			{
				_logger.LogWarning($"Error reading file \"{filePath}\". Error: {apiError}");
			}
		}
	}
}