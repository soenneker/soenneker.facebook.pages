using Soenneker.Facebook.Pages.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Facebook.Pages.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class FacebookPagesUtilTests : HostedUnitTest
{
    private readonly IFacebookPagesUtil _util;

    public FacebookPagesUtilTests(Host host) : base(host)
    {
        _util = Resolve<IFacebookPagesUtil>(true);
    }

    [Test]
    public void Default()
    {

    }
}
