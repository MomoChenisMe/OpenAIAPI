using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Runtime.Serialization;

namespace OpenAIAPI.Filters
{
    /// <summary>
    /// 在Swagger UI上隱藏具有IgnoreDataMember屬性的屬性的Schema過濾器。透過這個過濾器，可以過濾掉在API文件中不應顯示的屬性，使API文件更清晰易讀。
    /// </summary>
    public class SwaggerSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema?.Properties == null)
            {
                return;
            }

            var ignoreDataMemberProperties = context.Type.GetProperties()
                .Where(t => t.GetCustomAttribute<IgnoreDataMemberAttribute>() != null);

            foreach (var ignoreDataMemberProperty in ignoreDataMemberProperties)
            {
                var propertyToHide = schema.Properties.Keys
                    .SingleOrDefault(x => x.ToLower() == ignoreDataMemberProperty.Name.ToLower());

                if (propertyToHide != null)
                {
                    schema.Properties.Remove(propertyToHide);
                }
            }
        }
    }

    /// <summary>
    /// 在Swagger UI上替換BasePath的過濾器。透過這個過濾器，可以將API的BasePath更改為指定的值，以便在Swagger UI上正確顯示所有端點的路徑。
    /// </summary>
    public class SwaggerBasePathFilter : IDocumentFilter
    {
        private readonly string _basePath;

        public SwaggerBasePathFilter(string basePath)
        {
            _basePath = basePath;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Servers.Add(new OpenApiServer { Url = _basePath });
        }
    }

    /// <summary>
    /// 在Swagger UI上添加安全性要求的過濾器。透過這個過濾器，可以在操作或控制器上設置[Authorize]屬性後，將安全要求添加到Swagger UI上。若操作或控制器有[AllowAnonymous]屬性，則不會應用任何安全要求。
    /// </summary>
    public class SwaggerSecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // 如果操作或控制器有 [AllowAnonymous] 屬性，則不應用任何安全要求
            var hasAllowAnonymous = context.MethodInfo.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>().Any()
                || context.MethodInfo.DeclaringType.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>().Any();

            if (hasAllowAnonymous)
            {
                operation.Security = new List<OpenApiSecurityRequirement>();
                return;
            }

            // 如果操作或控制器有 [Authorize] 屬性，則應用安全要求
            var hasAuthorize = context.MethodInfo.GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>().Any()
                || context.MethodInfo.DeclaringType.GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>().Any();

            if (hasAuthorize)
            {
                operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                }
            };
            }
        }
    }
}
