using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 *  Collision Info
 *
 *  Wrapper of simplified collision data needed by agent.
 *
 *  Authored By Ryan Maugin (@ryanmaugv1)
 */
public class CollisionInfo {
    /// Number of current collisions with object.
    public int CollisionCount = 0;
    /// Tag of objects we are colliding with (duplicates allowed).
    public List<string> CollisionTags = new List<string>();

    
    /// Add new collision information.
    public void AddCollision(string tag) {
        CollisionCount += 1;
        CollisionTags.Add(tag);
    }

    /// Remove collision information.
    public void RemoveCollision(string tag) {
        CollisionCount -= 1;
        RemoveTag(tag);
    }

    /// Return whether we are colliding with any objects.
    public bool Colliding() => CollisionCount == 0 ? false : true;

    /// Return whether tag exists in collision tags list.
    public bool CheckTagExists(string tag) => CollisionTags.Exists(ctag => ctag == tag);
    
    /// Removes a tag from tag collision tags list.
    private void RemoveTag(string tag) {
        int i = CollisionTags.FindIndex(ctag => ctag == tag);
        CollisionTags.RemoveAt(i);
    }

    /// Debug logs state of this object with nice formatting.
    public void DebugLogState() {
        Debug.Log(
            "Count: " + CollisionCount +
            "\nTags : " + string.Join(", ", CollisionTags));
    }
}
