using Microsoft.AspNet.OData;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Services.App.Filters
{
    public class CustomSwaggerDocumentFilter : IDocumentFilter
    {
        public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context)
        {
            Assembly assembly = typeof(ODataController).Assembly;
            var thisAssemblyTypes = Assembly.GetExecutingAssembly().GetTypes().ToList();
            var odatacontrollers = thisAssemblyTypes.Where(t => t.BaseType == typeof(ODataController)).ToList();
            var odatamethods = new List<KeyValuePair<HttpMethodType, string>>
            {
                new KeyValuePair<HttpMethodType, string>( HttpMethodType.Get,"HttpGetAttribute"),
                new KeyValuePair<HttpMethodType, string>( HttpMethodType.Post,"HttpPostAttribute"),
                new KeyValuePair<HttpMethodType, string>( HttpMethodType.Put,"HttpPutAttribute"),
                new KeyValuePair<HttpMethodType, string>( HttpMethodType.Patch,"HttpPatchAttribute")
            };

            foreach (var odataContoller in odatacontrollers)  // this the OData controllers in the API
            {
                var methods = odataContoller.GetMethods().Where(a => a.DeclaringType.AssemblyQualifiedName.StartsWith("[AssemblyNameWhereControllersAre]", StringComparison.Ordinal)).ToList();
                if (!methods.Any())
                    continue; // next controller
                var odataPathItem = new PathItem();
                var controllerName = odataContoller.Name.Replace("Controller", "");
                var path = "/" + "odata" + "/" + controllerName;// + sb.ToString();
                foreach (MethodInfo method in methods)  // this is all of the methods in controller (e.g. GET, POST, PUT, etc)
                {
                    var methodType = odatamethods.FirstOrDefault(x => method.CustomAttributes.Any(n => n.AttributeType.Name == x.Value)).Key;
                    var parameterInfo = method.GetParameters();
                    var operation = new Operation();
                    List<IParameter> parameterList = new List<IParameter>();
                    if (methodType == HttpMethodType.Get)
                    {
                        parameterList.Add(new QueryParameter { In = "query", Name = "$expand", Description = "Expands related entities inline.", Required = false });
                        parameterList.Add(new QueryParameter { In = "query", Name = "$filter", Description = "Filters the results, based on a Boolean condition.", Required = false });
                        parameterList.Add(new QueryParameter { In = "query", Name = "$select", Description = "Selects which properties to include in the response.", Required = false });
                        parameterList.Add(new QueryParameter { In = "query", Name = "$orderby", Description = "Sorts the results.", Required = false });
                        parameterList.Add(new QueryParameter { In = "query", Name = "$top", Description = "Returns only the first n results.", Required = false });
                        parameterList.Add(new QueryParameter { In = "query", Name = "$skip", Description = "Skips the first n results.", Required = false });
                    }
                    foreach (ParameterInfo pi in parameterInfo)
                    {
                        var schema = new Schema { Ref = $"#/definitions/{pi.ParameterType.Name}", Type = pi.ParameterType.ToString() };
                        parameterList.Add(new BodyParameter { Schema = schema, Name = pi.ParameterType.Name });
                        if (!swaggerDoc.Definitions.ContainsKey(pi.ParameterType.Name))
                        {
                            context.SchemaRegistry.GetOrRegister(pi.ParameterType);
                        }
                    }
                    operation.Parameters = parameterList;
                    // The odata methods will be listed under a heading called OData in the swagger doc
                    operation.Tags = new List<string> { controllerName };
                    operation.OperationId = "OData_" + controllerName + methodType.ToString();

                    // This hard-coded for now, set it to use XML comments if you want
                    operation.Summary = "Summary about method / data";
                    operation.Description = "Description / options for the call.";

                    operation.Consumes = new List<string>();
                    operation.Produces = new List<string> { "application/json", "application/atom+xml", "text/json", "application/xml", "text/xml" };
                    operation.Deprecated = false;

                    var response = new Response() { Description = method.ReturnType.Name };
                    response.Schema = new Schema { Type = "array", Items = context.SchemaRegistry.GetOrRegister(method.ReturnType) };
                    operation.Responses = new Dictionary<string, Response> { { "200", response } };

                    var security = GetSecurityForOperation(odataContoller);
                    if (security != null)
                        operation.Security = new List<IDictionary<string, IEnumerable<string>>> { security };
                    switch (methodType)
                    {
                        case HttpMethodType.Get:
                            {
                                odataPathItem.Get = operation;
                            }
                            break;

                        case HttpMethodType.Patch:
                            odataPathItem.Patch = operation;
                            break;

                        case HttpMethodType.Post:
                            odataPathItem.Post = operation;
                            break;

                        case HttpMethodType.Put:
                            odataPathItem.Put = operation;
                            break;
                    }
                }
                swaggerDoc.Paths.Add(path, odataPathItem);
            }
        }

        private enum HttpMethodType
        {
            Get,
            Post,
            Patch,
            Put
        }

        private Dictionary<string, IEnumerable<string>> GetSecurityForOperation(MemberInfo odataContoller)
        {
            Dictionary<string, IEnumerable<string>> securityEntries = null;
            if (odataContoller.GetCustomAttribute(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute)) != null)
            {
                securityEntries = new Dictionary<string, IEnumerable<string>> { { "oauth2", new[] { "actioncenter" } } };
            }
            return securityEntries;
        }
    }

    public class QueryParameter : IParameter
    {
        public string Name { get; set; }
        public string In { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }

        public Dictionary<string, object> Extensions => null;
    }
}
