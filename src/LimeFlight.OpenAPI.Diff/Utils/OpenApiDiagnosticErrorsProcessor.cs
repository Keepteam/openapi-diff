using System;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;

namespace LimeFlight.OpenAPI.Diff.Utils
{
	public class OpenApiDiagnosticErrorsProcessor
	{
		public virtual void ProcessingErrors(string filePath, ICollection<OpenApiError> openApiErrors)
		{
			throw new Exception($"Error reading file \"{filePath}\". Error: {string.Join(", ", openApiErrors)}");
		}
	}
}