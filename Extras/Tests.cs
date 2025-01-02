using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.System;

namespace Dynamic_Lighting_Key_Indicator.Extras;
internal class Tests
{
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

        // Test loop with delay to flash the incides with no keys a few times
        for (int i = 0; i < 5; i++)
        {
            ColorSetter.SetKeysListToColor(lampArray, indicesWithoutKeys, Colors.Green);
            System.Threading.Thread.Sleep(1000);
            ColorSetter.SetKeysListToColor(lampArray, indicesWithoutKeys, Colors.White);
        }
    }
}
