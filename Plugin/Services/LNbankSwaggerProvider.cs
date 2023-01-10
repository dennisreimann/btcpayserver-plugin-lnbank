using System.IO;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNbankSwaggerProvider : ISwaggerProvider
{
    private readonly IFileProvider _fileProvider;

    public LNbankSwaggerProvider(IWebHostEnvironment webHostEnvironment)
    {
        _fileProvider = webHostEnvironment.WebRootFileProvider;
    }

    public async Task<JObject> Fetch()
    {
        JObject json = new();
        IFileInfo fi = _fileProvider.GetFileInfo("Resources/swagger/v1/swagger.template.lnbank.json");
        await using Stream stream = fi.CreateReadStream();
        using StreamReader reader = new StreamReader(fi.CreateReadStream());
        json.Merge(JObject.Parse(await reader.ReadToEndAsync()));
        return json;
    }
}
