// Escape-From-Duckov-Coop-Mod-Preview
// Copyright (C) 2025  Mr.sans and InitLoader's team
//
// This program is not a free software.
// It's distributed under a license based on AGPL-3.0,
// with strict additional restrictions:
//  YOU MUST NOT use this software for commercial purposes.
//  YOU MUST NOT use this software to run a headless game server.
//  YOU MUST include a conspicuous notice of attribution to
//  Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview as the original author.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

using System.Text;

namespace EscapeFromDuckovCoopMod;

[DisallowMultipleComponent]
public class NetDestructibleTag : MonoBehaviour
{
    public uint id;

    private void Awake()
    {
        id = ComputeStableId(gameObject);
    }

    public static uint ComputeStableId(GameObject go)
    {
        var sceneIndex = go.scene.buildIndex;

        var t = go.transform;
        var stack = new Stack<Transform>();
        while (t != null)
        {
            stack.Push(t);
            t = t.parent;
        }

        var sb = new StringBuilder(256);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            sb.Append('/').Append(cur.name).Append('#').Append(cur.GetSiblingIndex());
        }

        var p = go.transform.position;
        var px = Mathf.RoundToInt(p.x * 100f);
        var py = Mathf.RoundToInt(p.y * 100f);
        var pz = Mathf.RoundToInt(p.z * 100f);

        var key = $"{sceneIndex}:{sb}:{px},{py},{pz}";

        // FNV1a-32 可能有用 by:InitLoader 
        unchecked
        {
            var hash = 2166136261;
            for (var i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }

            return hash == 0 ? 1u : hash;
        }
    }
}