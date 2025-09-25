using DeepEqual;

namespace DeepEqual.RewrittenTests;

public static partial class DeepOpsConfig
{
    public static void Configure(DeepOpsBuilder b)
    {
        // Root registration
        b.ForRoot<Domain.Order>()
         .GenerateDiff()
         .GenerateDelta()
         .IncludeInternals()
         // Unordered keyed collections for OrderLine by SKU
         .OrderInsensitiveFor<Domain.OrderLine>(true)
         .KeyFor<Domain.OrderLine>(x => x.Sku);

        // adopt external root
        b.ForExternal<Domain.ExternalRoot>()
         .AdoptAsRoot(generateDelta: true, generateDiff: true);
    }
}