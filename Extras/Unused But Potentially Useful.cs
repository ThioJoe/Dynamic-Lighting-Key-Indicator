// Was going to use this in MainWindow.xaml.cs to compare current config and saved config but ended up doing it a different way
private static bool CompareNestedObjects(object obj1, object obj2)
{
    // Handle nulls
    if (obj1 == null && obj2 == null)
        return true;
    if (obj1 == null || obj2 == null)
        return false;

    // Compare types
    var type1 = obj1.GetType();
    var type2 = obj2.GetType();
    if (type1 != type2)
        return false;

    // If it's a primitive type or string, compare directly
    if (type1.IsPrimitive || obj1 is string || obj1 is decimal)
        return obj1.Equals(obj2);

    // If it's a collection, compare each item
    if (typeof(IEnumerable).IsAssignableFrom(type1))
    {
        var enum1 = ((IEnumerable)obj1).GetEnumerator();
        var enum2 = ((IEnumerable)obj2).GetEnumerator();

        while (true)
        {
            bool hasNext1 = enum1.MoveNext();
            bool hasNext2 = enum2.MoveNext();

            if (hasNext1 != hasNext2)
                return false;
            if (!hasNext1)
                break;

            if (!CompareNestedObjects(enum1.Current, enum2.Current))
                return false;
        }
        return true;
    }

    // For other types, compare their properties recursively
    foreach (var property in type1.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        var value1 = property.GetValue(obj1);
        var value2 = property.GetValue(obj2);

        if (!CompareNestedObjects(value1, value2))
            return false;
    }
    return true;
}