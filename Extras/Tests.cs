using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.System;

namespace Dynamic_Lighting_Key_Indicator.Extras;
internal class Tests
{

    public static List<VirtualKey> GetVKsForAllLamps(LampArray lampArray)
    {
        List<(VirtualKey, int)> vkTuples = GetVKCodesTupleList();

        // Get the indices that correspond to each virtual key
        List<(VirtualKey, int[])> keyIndicesDict = [];
        foreach (var (vk, vkInt) in vkTuples)
        {
            int[] indices = lampArray.GetIndicesForKey(vk);
            if (indices.Length > 0)
                keyIndicesDict.Add((vk, indices));
        }

        // Test all possible VK raw ints to see if they have a corresponding key
        Dictionary<int, int[]> rawVKCodesMatchingOnly = [];
        for (int i = 0; i < 256; i++)
        {
            VirtualKey vk = (VirtualKey)i;
            int[] indices = lampArray.GetIndicesForKey(vk);
            if (indices.Length > 0)
            {
                rawVKCodesMatchingOnly.Add(i, indices);
            }
        }

        // Convert the raw VK codes to a list of VKs
        List<VirtualKey> rawVKCodesMatchingOnlyList = [];
        foreach (var (vkInt, indices) in rawVKCodesMatchingOnly)
        {
            rawVKCodesMatchingOnlyList.Add((VirtualKey)vkInt);
        }

        return rawVKCodesMatchingOnlyList;

    }

    public static List<(VirtualKey, int)> GetVKCodesTupleList()
    {
        List<VirtualKey> allVKs = (List<VirtualKey>)ExtendedVirtualKeySet.GetAllKeys().ToList();
        List<(VirtualKey, int)> VKCodesTupleList = [];
        foreach (VirtualKey vk in allVKs)
        {
            VKCodesTupleList.Add((vk, (int)vk));
        }
        return VKCodesTupleList;
    }

    public static void GetIndicesPurposesAndUnknownKeys(LampArray lampArray)
    {
        // Basic list of all the indices
        List<int> allIndices = [];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            allIndices.Add(i);
        }

        List<VirtualKey> allVKs = [];
        //allVKs = Enum.GetValues<VirtualKey>().ToList();
        allVKs = (List<VirtualKey>)ExtendedVirtualKeySet.GetAllKeys().ToList();

        // Get the list of all virtual key codes and their corresponding integer values
        List<(VirtualKey, int)> VKCodesTupleList = [];
        foreach (VirtualKey vk in allVKs)
        {
            VKCodesTupleList.Add((vk, (int)vk));
        }

        // Get the indices tagged for each purpose
        var purposes = Enum.GetValues<LampPurposes>().Cast<LampPurposes>().ToArray();
        Dictionary<LampPurposes, int[]> purposeIndicesDict = [];
        foreach (var purpose in purposes)
        {
            purposeIndicesDict.Add(purpose, lampArray.GetIndicesForPurposes(purpose));
        }

        // Get the indices that correspond to each virtual key
        List<(VirtualKey, int[])> keyIndicesDict = [];
        List<VirtualKey> keysWithNoCorrespondingLight = [];
        foreach (var (vk, vkInt) in VKCodesTupleList)
        {
            int[] indices = lampArray.GetIndicesForKey(vk);
            if (indices.Length > 0)
                keyIndicesDict.Add((vk, indices));
            else
                keysWithNoCorrespondingLight.Add(vk);
        }

        // Create dictionary of purposes and the list of virtual keys included in that purpose
        Dictionary<LampPurposes, List<VirtualKey>> purposeKeysDict = [];
        foreach (var (purpose, indices) in purposeIndicesDict)
        {
            List<VirtualKey> keys = [];
            foreach (var (vk, vkInt) in VKCodesTupleList)
            {
                int[] keyIndices = lampArray.GetIndicesForKey(vk);
                if (keyIndices.Length > 0 && keyIndices.Any(index => indices.Contains(index)))
                    keys.Add(vk);
            }
            purposeKeysDict.Add(purpose, keys);
        }

        // Find indices that don't match to any key
        HashSet<int> indicesWithKeys = [];
        foreach (var (_, indices) in keyIndicesDict)
        {
            foreach (int index in indices)
            {
                indicesWithKeys.Add(index);
            }
        }

        List<int> indicesWithoutKeys = allIndices.Except(indicesWithKeys).ToList();

        // Test all possible VK raw ints to see if they have a corresponding key
        Dictionary<int, int[]> rawVKCodesDict = [];
        Dictionary<int, int[]> rawVKCodesMatchingOnly = [];
        List<int> unknownVKCodes = [];
        for (int i = 0; i < 256; i++)
        {
            VirtualKey vk = (VirtualKey)i;
            int[] indices = lampArray.GetIndicesForKey(vk);
            rawVKCodesDict.Add(i, indices);
            if (indices.Length > 0)
            {
                rawVKCodesMatchingOnly.Add(i, indices);
                // Check if we know the key for the VK code
                if (!allVKs.Contains(vk))
                {
                    unknownVKCodes.Add(i);
                }
            }
        }

        List<(int, VirtualKey)> indexToVKTupleList = [];
        foreach (var (vk, vkInt) in VKCodesTupleList)
        {
            int[] indices = lampArray.GetIndicesForKey(vk);
            foreach (int index in indices)
            {
                indexToVKTupleList.Add((index, vk));
            }
        }

        // Sort the list of tuples by index
        indexToVKTupleList.Sort((x, y) => x.Item1.CompareTo(y.Item1));

        List<LampInfo> allLampInfo = [];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            allLampInfo.Add(lampArray.GetLampInfo(i));
        }

        // Test loop with delay to flash the incides with no keys a few times
        //for (int i = 0; i < 5; i++)
        //{
        //    ColorSetter.SetKeysListToColor(lampArray, indicesWithoutKeys, Colors.Green);
        //    System.Threading.Thread.Sleep(1000);
        //    ColorSetter.SetKeysListToColor(lampArray, indicesWithoutKeys, Colors.White);
        //}
    }

    public static void SetAllKeyColorsAsList(LampArray lampArray, Color color)
    {
        List<int> allIndices = [];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            allIndices.Add(i);
        }

        lampArray.SetSingleColorForIndices(color, allIndices.ToArray());
    }



    public static void SetAllColorsUsingVK(LampArray lampArray, Color color)
    {
        List<VirtualKey> VKList = GetVKsForAllLamps(lampArray);
        Color[] colors = new Color[VKList.Count];
        for (int i = 0; i < VKList.Count; i++)
        {
            colors[i] = color;
        }

        lampArray.SetColorsForKeys(colors, VKList.ToArray());
    }

    public static void SetAllKeysColorsAsPairedList(LampArray lampArray, Color color)
    {
        List<int> allIndices = [];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            allIndices.Add(i);
        }

        List<Color> allColors = new List<Color>();
        for (int i = 0; i < allIndices.Count; i++)
        {
            allColors.Add(color);
        }

        lampArray.SetColorsForIndices(allColors.ToArray(), allIndices.ToArray());
    }

    public static void SetOneKeyOneColorRestOtherKeyAnotherColor(LampArray lampArray, VirtualKey key, Color chosenKeyColor, Color otherColor)
    {
        List<int> allIndices = [];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            allIndices.Add(i);
        }

        // Get the index of the key
        int keyIndex = lampArray.GetIndicesForKey(key).FirstOrDefault();

        Color[] colors = new Color[lampArray.LampCount];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            if (i == keyIndex)
                colors[i] = chosenKeyColor;
            else
                colors[i] = otherColor;
        }

        lampArray.SetColorsForIndices(colors, allIndices.ToArray());
    }

    public static void SetOneKeyOneColorRestOtherKeyAnotherColor_UsingVK(LampArray lampArray, VirtualKey key, Color chosenKeyColor, Color otherColor)
    {
        List<VirtualKey> VKList = GetVKsForAllLamps(lampArray);
        Color[] colors = new Color[VKList.Count];
        for (int i = 0; i < VKList.Count; i++)
        {
            if (VKList[i] == key)
                colors[i] = chosenKeyColor;
            else
                colors[i] = otherColor;
        }

        lampArray.SetColorsForKeys(colors, VKList.ToArray());
    }

    public static void SetOneIndexOneColorOthersAnotherColor(LampArray lampArray, int keyIndex, Color chosenKeyColor, Color otherColor)
    {
        List<int> allIndices = [];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            allIndices.Add(i);
        }
        Color[] colors = new Color[lampArray.LampCount];
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            if (i == keyIndex)
                colors[i] = chosenKeyColor;
            else
                colors[i] = otherColor;
        }
        lampArray.SetColorsForIndices(colors, allIndices.ToArray());
    }

    public static void LoopThroughAllKeyIndexes(LampArray lampArray, Color color1, Color color2)
    {
        for (int i = 0; i < lampArray.LampCount; i++)
        {
            Debug.WriteLine("Setting index: " + i);
            SetOneIndexOneColorOthersAnotherColor(lampArray, i, color1, color2);
            System.Threading.Thread.Sleep(500);
        }
    }

    public static void LoopThroughRangeOfIndexes(LampArray lampArray, int startIndex, int endIndex, Color color1, Color color2)
    {
        for (int i = startIndex; i <= endIndex; i++)
        {
            Debug.WriteLine("Setting index: " + i);
            SetOneIndexOneColorOthersAnotherColor(lampArray, i, color1, color2);
            System.Threading.Thread.Sleep(1000);
        }
    }
}
