using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using DPUruNet;

namespace VidaFit.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class DPUruNetExplorerController : ControllerBase
    {
        // ════════════════════════════════════════════════════════════════════
        // EXPLORAR TODOS LOS TIPOS EN DPUruNet
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("explore-types")]
        public IActionResult ExploreTypes()
        {
            try
            {
                var assembly = typeof(Reader).Assembly;
                var allTypes = assembly.GetTypes()
                    .Where(t => t.IsPublic)
                    .OrderBy(t => t.Name)
                    .Select(t => new
                    {
                        name = t.Name,
                        fullName = t.FullName,
                        @namespace = t.Namespace,
                        isClass = t.IsClass,
                        isInterface = t.IsInterface,
                        isEnum = t.IsEnum,
                        isStatic = t.IsAbstract && t.IsSealed
                    })
                    .ToList();

                var grouped = allTypes.GroupBy(t => t.@namespace)
                    .Select(g => new
                    {
                        @namespace = g.Key,
                        count = g.Count(),
                        types = g.Select(t => t.name).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    assemblyName = assembly.GetName().Name,
                    version = assembly.GetName().Version?.ToString(),
                    totalTypes = allTypes.Count,
                    namespaces = grouped,
                    allTypes = allTypes
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BUSCAR CLASES/MÉTODOS RELACIONADOS CON FMD
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("search-fmd")]
        public IActionResult SearchFmd()
        {
            try
            {
                var assembly = typeof(Reader).Assembly;

                // Buscar tipos que contengan "FMD" o "Feature"
                var fmdTypes = assembly.GetTypes()
                    .Where(t => t.IsPublic && (
                        t.Name.Contains("Fmd", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("Feature", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("Template", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("Minutiae", StringComparison.OrdinalIgnoreCase)
                    ))
                    .Select(t => new
                    {
                        name = t.Name,
                        fullName = t.FullName,
                        @namespace = t.Namespace,
                        isClass = t.IsClass,
                        methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                            .Where(m => m.DeclaringType == t)
                            .Select(m => new
                            {
                                name = m.Name,
                                isStatic = m.IsStatic,
                                returnType = m.ReturnType.Name,
                                parameters = m.GetParameters().Select(p => new
                                {
                                    name = p.Name,
                                    type = p.ParameterType.Name
                                }).ToList()
                            }).ToList(),
                        properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Select(p => new
                            {
                                name = p.Name,
                                type = p.PropertyType.Name,
                                canRead = p.CanRead,
                                canWrite = p.CanWrite
                            }).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    found = fmdTypes.Count,
                    types = fmdTypes
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EXPLORAR CLASE Fid (Fingerprint Image Data)
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("explore-fid")]
        public IActionResult ExploreFid()
        {
            try
            {
                var fidType = typeof(Fid);

                var info = new
                {
                    name = fidType.Name,
                    fullName = fidType.FullName,
                    @namespace = fidType.Namespace,

                    constructors = fidType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .Select(c => new
                        {
                            parameters = c.GetParameters().Select(p => new
                            {
                                name = p.Name,
                                type = p.ParameterType.Name
                            }).ToList()
                        }).ToList(),

                    methods = fidType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .Where(m => m.DeclaringType == fidType)
                        .Select(m => new
                        {
                            name = m.Name,
                            isStatic = m.IsStatic,
                            returnType = m.ReturnType.Name,
                            parameters = m.GetParameters().Select(p => new
                            {
                                name = p.Name,
                                type = p.ParameterType.Name
                            }).ToList()
                        }).ToList(),

                    properties = fidType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => new
                        {
                            name = p.Name,
                            type = p.PropertyType.Name,
                            canRead = p.CanRead,
                            canWrite = p.CanWrite
                        }).ToList()
                };

                return Ok(new
                {
                    success = true,
                    fid = info
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EXPLORAR CLASE Fmd (Fingerprint Minutiae Data)
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("explore-fmd")]
        public IActionResult ExploreFmd()
        {
            try
            {
                var fmdType = typeof(Fmd);

                var info = new
                {
                    name = fmdType.Name,
                    fullName = fmdType.FullName,
                    @namespace = fmdType.Namespace,

                    constructors = fmdType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .Select(c => new
                        {
                            parameters = c.GetParameters().Select(p => new
                            {
                                name = p.Name,
                                type = p.ParameterType.Name
                            }).ToList()
                        }).ToList(),

                    methods = fmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .Where(m => m.DeclaringType == fmdType)
                        .Select(m => new
                        {
                            name = m.Name,
                            isStatic = m.IsStatic,
                            returnType = m.ReturnType.Name,
                            parameters = m.GetParameters().Select(p => new
                            {
                                name = p.Name,
                                type = p.ParameterType.Name
                            }).ToList()
                        }).ToList(),

                    properties = fmdType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => new
                        {
                            name = p.Name,
                            type = p.PropertyType.Name,
                            canRead = p.CanRead,
                            canWrite = p.CanWrite
                        }).ToList()
                };

                return Ok(new
                {
                    success = true,
                    fmd = info
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BUSCAR CLASE FeatureExtraction
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("explore-feature-extraction")]
        public IActionResult ExploreFeatureExtraction()
        {
            try
            {
                var assembly = typeof(Reader).Assembly;

                // Buscar clase FeatureExtraction
                var featureType = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name.Contains("Feature", StringComparison.OrdinalIgnoreCase) &&
                                        t.Name.Contains("Extract", StringComparison.OrdinalIgnoreCase));

                if (featureType == null)
                {
                    // Buscar cualquier clase con métodos relacionados a FMD
                    var typesWithFmdMethods = assembly.GetTypes()
                        .Where(t => t.IsPublic)
                        .Select(t => new
                        {
                            type = t,
                            fmdMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Where(m => m.ReturnType.Name.Contains("Fmd") ||
                                           m.GetParameters().Any(p => p.ParameterType.Name.Contains("Fmd")))
                                .ToList()
                        })
                        .Where(x => x.fmdMethods.Any())
                        .ToList();

                    return Ok(new
                    {
                        success = true,
                        featureExtractionFound = false,
                        message = "No se encontró clase FeatureExtraction, pero aquí están las clases con métodos FMD",
                        typesWithFmdMethods = typesWithFmdMethods.Select(x => new
                        {
                            typeName = x.type.Name,
                            fullName = x.type.FullName,
                            methods = x.fmdMethods.Select(m => new
                            {
                                name = m.Name,
                                returnType = m.ReturnType.Name,
                                parameters = m.GetParameters().Select(p => new
                                {
                                    name = p.Name,
                                    type = p.ParameterType.Name
                                }).ToList()
                            }).ToList()
                        }).ToList()
                    });
                }

                var info = new
                {
                    name = featureType.Name,
                    fullName = featureType.FullName,
                    @namespace = featureType.Namespace,

                    methods = featureType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .Where(m => m.DeclaringType == featureType)
                        .Select(m => new
                        {
                            name = m.Name,
                            isStatic = m.IsStatic,
                            returnType = m.ReturnType.Name,
                            parameters = m.GetParameters().Select(p => new
                            {
                                name = p.Name,
                                type = p.ParameterType.Name
                            }).ToList()
                        }).ToList()
                };

                return Ok(new
                {
                    success = true,
                    featureExtractionFound = true,
                    featureExtraction = info
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BUSCAR CLASE Comparison
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("explore-comparison")]
        public IActionResult ExploreComparison()
        {
            try
            {
                var assembly = typeof(Reader).Assembly;

                var comparisonType = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase));

                if (comparisonType == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No se encontró clase Comparison"
                    });
                }

                var info = new
                {
                    name = comparisonType.Name,
                    fullName = comparisonType.FullName,
                    @namespace = comparisonType.Namespace,

                    methods = comparisonType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .Where(m => m.DeclaringType == comparisonType)
                        .Select(m => new
                        {
                            name = m.Name,
                            isStatic = m.IsStatic,
                            returnType = m.ReturnType.Name,
                            parameters = m.GetParameters().Select(p => new
                            {
                                name = p.Name,
                                type = p.ParameterType.Name
                            }).ToList()
                        }).ToList()
                };

                return Ok(new
                {
                    success = true,
                    comparison = info
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PROBAR EXTRACCIÓN DE FMD (intentar todos los métodos posibles)
        // ════════════════════════════════════════════════════════════════════
        [HttpPost("try-fmd-extraction")]
        public IActionResult TryFmdExtraction()
        {
            try
            {
                var results = new List<object>();

                // Método 1: Intentar con FeatureExtraction (si existe)
                try
                {
                    var assembly = typeof(Reader).Assembly;
                    var featureType = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name.Contains("Feature") && t.Name.Contains("Extract"));

                    if (featureType != null)
                    {
                        results.Add(new
                        {
                            method = "FeatureExtraction encontrado",
                            success = true,
                            type = featureType.FullName,
                            staticMethods = featureType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Select(m => m.Name)
                                .ToList()
                        });
                    }
                    else
                    {
                        results.Add(new
                        {
                            method = "FeatureExtraction",
                            success = false,
                            message = "Clase no encontrada"
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { method = "FeatureExtraction", success = false, error = ex.Message });
                }

                return Ok(new
                {
                    success = true,
                    results = results,
                    recommendation = "Revisa los métodos disponibles en la clase encontrada"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }
    }
}