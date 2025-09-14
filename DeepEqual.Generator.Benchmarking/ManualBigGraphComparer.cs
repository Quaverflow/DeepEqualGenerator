namespace DeepEqual.Generator.Benchmarking;

static class ManualBigGraphComparer
{
    public static bool AreEqual(BigGraph? a, BigGraph? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (!string.Equals(a.Title, b.Title, StringComparison.Ordinal))
        {
            return false;
        }

        if (!OrgEqual(a.Org, b.Org))
        {
            return false;
        }

        if (!ListEqual(a.Catalog, b.Catalog, ProductEqual))
        {
            return false;
        }

        if (!ListEqual(a.Customers, b.Customers, CustomerEqual))
        {
            return false;
        }

        if (!DictOrgEqual(a.OrgIndex, b.OrgIndex))
        {
            return false;
        }

        if (!ManualValueComparer.AreEqual(a.Meta, b.Meta))
        {
            return false;
        }

        return true;

        static bool OrgEqual(OrgNode? a, OrgNode? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (a.Role != b.Role)
            {
                return false;
            }

            if (!ListEqual(a.Reports, b.Reports, OrgEqual))
            {
                return false;
            }

            if (!ManualValueComparer.AreEqual(a.Extra, b.Extra))
            {
                return false;
            }

            return true;
        }

        static bool ProductEqual(Product? a, Product? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Sku != b.Sku || a.Name != b.Name)
            {
                return false;
            }

            if (a.Price != b.Price || a.Introduced != b.Introduced)
            {
                return false;
            }

            if (!ManualValueComparer.AreEqual(a.Attributes, b.Attributes))
            {
                return false;
            }

            return true;
        }

        static bool OrderLineEqual(OrderLine? a, OrderLine? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Sku == b.Sku && a.Qty == b.Qty && a.LineTotal == b.LineTotal;
        }

        static bool OrderEqual(Order? a, Order? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Id != b.Id || a.Created != b.Created)
            {
                return false;
            }

            if (!ListEqual(a.Lines, b.Lines, OrderLineEqual))
            {
                return false;
            }

            if (!DictEqual(a.Meta, b.Meta))
            {
                return false;
            }

            if (!ManualValueComparer.AreEqual(a.Extra, b.Extra))
            {
                return false;
            }

            return true;
        }

        static bool CustomerEqual(Customer? a, Customer? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Id != b.Id || a.FullName != b.FullName)
            {
                return false;
            }

            if (!ListEqual(a.Orders, b.Orders, OrderEqual))
            {
                return false;
            }

            if (!ManualValueComparer.AreEqual(a.Profile, b.Profile))
            {
                return false;
            }

            return true;
        }

        static bool DictEqual(Dictionary<string, string>? a, Dictionary<string, string>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !string.Equals(v, bv, StringComparison.Ordinal))
                {
                    return false;
                }

            return true;
        }

        static bool DictOrgEqual(Dictionary<string, OrgNode>? a, Dictionary<string, OrgNode>? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out var bv) || !OrgEqual(v, bv))
                {
                    return false;
                }

            return true;
        }

        static bool ListEqual<T>(List<T>? a, List<T>? b, Func<T?, T?, bool> eq)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
                if (!eq(a[i], b[i]))
                {
                    return false;
                }

            return true;
        }
    }
}