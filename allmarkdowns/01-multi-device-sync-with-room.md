# I Tried to Build Multi-Device Sync with Room (So You Don't Have To)

Look, I get it. Room is great. It's clean, it's type-safe, and it fits perfectly into the Android ecosystem. I've built probably a dozen apps with it, and I've never had complaints. Until last month.

My PM walks up and says, "Hey, can we sync this data across devices?" Sure, I thought. How hard could it be?

Spoiler: Very hard.

## The Plan (That Seemed Reasonable at 9 AM)

I had a simple notes app using Room. The schema was straightforward:

```kotlin
@Entity(tableName = "notes")
data class Note(
    @PrimaryKey val id: String = UUID.randomUUID().toString(),
    val title: String,
    val content: String,
    val lastModified: Long = System.currentTimeMillis()
)
```

My brilliant plan:
1. Set up a REST API on the backend
2. Send local changes to the server
3. Pull remote changes and merge them
4. Profit?

## Reality Check 1: The Manual API Hell

First problem: Room doesn't have any built-in sync. Everything is manual. So I had to:

- Track which notes were dirty (modified locally)
- Queue them for upload
- Handle network failures
- Retry failed uploads
- Deal with partial syncs
- Track sync state per note

I ended up adding fields to my entity:

```kotlin
@Entity(tableName = "notes")
data class Note(
    @PrimaryKey val id: String = UUID.randomUUID().toString(),
    val title: String,
    val content: String,
    val lastModified: Long = System.currentTimeMillis(),
    val syncStatus: SyncStatus = SyncStatus.PENDING,
    val lastSyncedAt: Long? = null,
    val isDirty: Boolean = false
)
```

Great. Now my clean entity is cluttered with sync metadata.

## Reality Check 2: Conflict Hell

Here's where things got fun. User edits "Meeting Notes" on their phone, then edits the same note on their tablet before the phone syncs. Which version wins?

My first attempt: "Last write wins based on timestamp."

Sounds reasonable, right? Except:
- Clocks aren't always in sync
- Network delays mean "last" is relative
- Users get really mad when their edits disappear

I tried version vectors. I tried vector clocks. I tried telling myself this was a learning experience.

```kotlin
// My attempt at conflict resolution
fun resolveConflict(local: Note, remote: Note): Note {
    return when {
        local.lastModified > remote.lastModified -> local
        local.lastModified < remote.lastModified -> remote
        else -> {
            // Same timestamp? Now what?
            // Merge content? Prompt user? Flip a coin?
            Log.e("Sync", "Help")
            remote // I gave up
        }
    }
}
```

## Reality Check 3: The Duplicate Row Problem

You know what's worse than losing data? Having two copies of the same note because the UUID generated on the phone doesn't match the ID the server assigned.

I had to implement ID mapping:

```kotlin
data class IdMapping(
    val localId: String,
    val remoteId: String
)
```

And a whole separate table to track it. My "simple" sync was now three tables deep.

## Reality Check 4: Race Conditions Everywhere

- User opens app while sync is running -> stale data
- User edits note during sync -> which version do we keep?
- Sync starts while user is editing -> UI flickers
- Multiple devices sync at once -> chaos

I added locks. I added queues. I added way too many `synchronized` blocks. My code was starting to look like a threading textbook gone wrong.

```kotlin
private val syncLock = ReentrantReadWriteLock()

suspend fun syncNotes() {
    syncLock.writeLock().lock()
    try {
        // 50 lines of sync logic
        // 30 lines of error handling
        // 10 lines of "TODO: fix this properly"
    } finally {
        syncLock.writeLock().unlock()
    }
}
```

## Breaking Point

After two weeks of this, I had:
- 800+ lines of sync code
- 15 different edge cases in my test suite
- A bug where notes would randomly duplicate
- Another bug where deletions wouldn't sync
- A third bug that only happened on Tuesdays (I'm not joking)

I started Googling alternatives.

## Enter Couchbase Lite

I'm not going to lie, I was skeptical. "Just use another database" felt like giving up. But I was desperate.

The setup:

```kotlin
// That's it. That's the entire initial setup.
val database = Database("myapp")
```

Adding sync:

```kotlin
val replicator = Replicator(
    ReplicatorConfigurationFactory.create(
        database = database,
        target = URLEndpoint(URI("ws://localhost:4984/mydb")),
        type = ReplicatorType.PUSH_AND_PULL,
        continuous = true
    )
)
replicator.start()
```

That's it. No sync status tracking. No conflict resolution hell. No duplicate IDs. It just... works.

## How It Actually Handles Conflicts

Remember my terrible conflict resolution code? Couchbase does this automatically:

```kotlin
// Define custom conflict resolver if you need it
val config = ReplicatorConfigurationFactory.create(
    database = database,
    target = target,
    conflictResolver = { conflict ->
        val local = conflict.localDocument
        val remote = conflict.remoteDocument
        // Actually merge the content intelligently
        // Or just pick one
        local // Your choice, but it won't lose data randomly
    }
)
```

But honestly? The default resolver is pretty smart. It maintains revision history and merges changes. I haven't had to write a custom resolver yet.

## What About the Learning Curve?

Not gonna lie, there's some new concepts:
- Documents instead of entities
- Revision IDs
- The whole CouchDB replication protocol

But compared to implementing sync from scratch? This is a weekend, not a month.

## The Numbers

**Room + Manual Sync:**
- 800+ lines of sync code
- 2 weeks of development
- Still had bugs
- Nervous about scaling

**Couchbase Lite:**
- ~50 lines for full sync setup
- 1 day to integrate
- Zero sync bugs so far
- Built-in conflict resolution

## Should You Switch?

Look, if you're building a single-device app that never needs to sync, stick with Room. It's lighter and simpler.

But if you're even *thinking* about:
- Multi-device sync
- Offline-first architecture
- Real-time collaboration
- Data conflicts between devices

Save yourself the pain. Use Couchbase Lite from the start.

## The Catch

There's always a catch:
- Slightly larger APK size
- Need to run Sync Gateway for server sync
- Different query syntax (though it's SQL-like)

For me? Totally worth it. My sync code went from "oh god why" to "oh, it just works."

## Final Thoughts

I spent two weeks trying to reinvent the wheel with Room. Turns out, other people have already solved this problem, and they did a better job than I ever would.

Sometimes the right tool isn't the one you're already comfortable with. Sometimes it's the one that actually solves your problem.

Now if you'll excuse me, I have 800 lines of sync code to delete.

---

*Have you tried implementing sync with Room? Did you succeed where I failed? Or did you also end up Googling "why is distributed systems so hard" at 2 AM? Let me know in the comments.*
