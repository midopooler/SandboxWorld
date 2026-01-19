# I Replaced Room With Couchbase Lite for a Todo App and I'm Not Sorry

Three months ago, I had a perfectly fine todo app. Room database, clean architecture, MVVM, all the buzzwords. It worked great.

Then my users started asking: "Can I use this on my tablet?" 

And just like that, my perfectly fine app needed a complete rewrite.

## The Original Stack

Standard Android setup:
- Room for local storage
- Retrofit for API calls
- WorkManager for background sync
- A whole lot of custom code holding it together

The entities looked clean:

```kotlin
@Entity(tableName = "todos")
data class TodoEntity(
    @PrimaryKey val id: String,
    val title: String,
    val description: String,
    val isCompleted: Boolean,
    val createdAt: Long,
    val updatedAt: Long
)

@Dao
interface TodoDao {
    @Query("SELECT * FROM todos")
    fun getAllTodos(): Flow<List<TodoEntity>>
    
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertTodo(todo: TodoEntity)
    
    @Update
    suspend fun updateTodo(todo: TodoEntity)
    
    @Delete
    suspend fun deleteTodo(todo: TodoEntity)
}
```

Classic Room. Nothing wrong with it.

## The Sync Problem

To add multi-device support, I needed:
1. A backend API to store todos
2. Logic to push local changes
3. Logic to pull remote changes
4. Conflict resolution when both happen
5. Handling of deletions across devices
6. Network state monitoring
7. Retry logic for failed syncs
8. UI feedback for sync status

I started building this. Got about 400 lines in. Then I stopped and thought: "There has to be a better way."

## The Decision to Switch

I looked at Firebase Realtime Database. Too much vendor lock-in.
I looked at manual REST API sync. Already tried that, it sucked.
Then I found Couchbase Lite.

The pitch: "Embedded NoSQL database with built-in sync."

I was skeptical. Replacing your database isn't something you do lightly. But the sync problem wasn't going away, and my manual implementation was already causing bugs.

So I did it.

## The Migration (Surprisingly Painless)

### Step 1: Add Couchbase Lite

```kotlin
// build.gradle
dependencies {
    // implementation "androidx.room:room-runtime:2.6.0" // Commented out for now
    implementation 'com.couchbase.lite:couchbase-lite-android:3.1.0'
}
```

### Step 2: Initialize Database

```kotlin
class TodoDatabase(context: Context) {
    private val database: Database
    
    init {
        CouchbaseLite.init(context)
        database = Database("todos")
    }
    
    companion object {
        @Volatile
        private var INSTANCE: TodoDatabase? = null
        
        fun getInstance(context: Context): TodoDatabase {
            return INSTANCE ?: synchronized(this) {
                INSTANCE ?: TodoDatabase(context).also { INSTANCE = it }
            }
        }
    }
}
```

No RoomDatabase.Builder. No migrations. Just... a database.

### Step 3: Migrate the Data Model

This was the interesting part. Room uses entities. Couchbase uses documents.

Old Room entity:

```kotlin
@Entity(tableName = "todos")
data class TodoEntity(
    @PrimaryKey val id: String,
    val title: String,
    val description: String,
    val isCompleted: Boolean,
    val createdAt: Long,
    val updatedAt: Long
)
```

New Couchbase document (as a data class):

```kotlin
data class Todo(
    val id: String = UUID.randomUUID().toString(),
    val title: String,
    val description: String,
    val isCompleted: Boolean = false,
    val createdAt: Long = System.currentTimeMillis(),
    val updatedAt: Long = System.currentTimeMillis(),
    val type: String = "todo" // For querying
)
```

Almost identical. The `type` field is a Couchbase convention for filtering different document types.

### Step 4: Replace the DAO

Old DAO was 80 lines. New repository:

```kotlin
class TodoRepository(private val database: Database) {
    
    fun createTodo(title: String, description: String): String {
        val todo = Todo(
            title = title,
            description = description
        )
        val doc = MutableDocument(todo.id, todo.toMap())
        database.save(doc)
        return todo.id
    }
    
    fun updateTodo(id: String, title: String, description: String, isCompleted: Boolean) {
        val doc = database.getDocument(id)?.toMutable() ?: return
        doc.setString("title", title)
        doc.setString("description", description)
        doc.setBoolean("isCompleted", isCompleted)
        doc.setLong("updatedAt", System.currentTimeMillis())
        database.save(doc)
    }
    
    fun deleteTodo(id: String) {
        val doc = database.getDocument(id) ?: return
        database.delete(doc)
    }
    
    fun getAllTodos(): Flow<List<Todo>> = callbackFlow {
        val query = QueryBuilder
            .select(SelectResult.all())
            .from(DataSource.database(database))
            .where(Expression.property("type").equalTo(Expression.string("todo")))
            .orderBy(Ordering.property("createdAt").descending())
        
        val token = query.addChangeListener { change ->
            val todos = change.results?.allResults()?.mapNotNull { 
                it.getDictionary(0)?.toTodo() 
            } ?: emptyList()
            trySend(todos)
        }
        
        // Send initial data
        val todos = query.execute().allResults().mapNotNull { 
            it.getDictionary(0)?.toTodo() 
        }
        trySend(todos)
        
        awaitClose { token.remove() }
    }
}
```

A bit more verbose than Room's generated code, but not by much. And I get live updates for free.

### Step 5: Helper Extensions

```kotlin
fun Todo.toMap(): Map<String, Any> = mapOf(
    "title" to title,
    "description" to description,
    "isCompleted" to isCompleted,
    "createdAt" to createdAt,
    "updatedAt" to updatedAt,
    "type" to type
)

fun Dictionary.toTodo(): Todo? {
    return try {
        Todo(
            id = getString("id") ?: return null,
            title = getString("title") ?: "",
            description = getString("description") ?: "",
            isCompleted = getBoolean("isCompleted"),
            createdAt = getLong("createdAt"),
            updatedAt = getLong("updatedAt")
        )
    } catch (e: Exception) {
        null
    }
}
```

Not generated, but straightforward.

### Step 6: Migrate Existing Data

I needed to move data from Room to Couchbase. One-time migration:

```kotlin
suspend fun migrateFromRoom(roomDao: TodoDao, couchbaseDb: Database) {
    val roomTodos = roomDao.getAllTodosOneShot() // Non-Flow version
    
    roomTodos.forEach { roomTodo ->
        val doc = MutableDocument(roomTodo.id)
        doc.setString("title", roomTodo.title)
        doc.setString("description", roomTodo.description)
        doc.setBoolean("isCompleted", roomTodo.isCompleted)
        doc.setLong("createdAt", roomTodo.createdAt)
        doc.setLong("updatedAt", roomTodo.updatedAt)
        doc.setString("type", "todo")
        
        couchbaseDb.save(doc)
    }
    
    Log.d("Migration", "Migrated ${roomTodos.size} todos")
}
```

Ran once on app startup if a flag wasn't set. Smooth.

## What I Lost

Let me be honest about the tradeoffs:

1. **Type safety**: Room's compile-time SQL verification is nice. Couchbase is more dynamic.
2. **Generated code**: No auto-generated DAOs. You write the boilerplate.
3. **Familiar SQL**: Couchbase uses N1QL (SQL-ish but different).
4. **Android-specific docs**: Less Stack Overflow for Couchbase compared to Room.

## What I Gained

But here's what made it worth it:

### 1. Built-in Sync

```kotlin
class SyncManager(private val database: Database) {
    private var replicator: Replicator? = null
    
    fun startSync(username: String) {
        val endpoint = URLEndpoint(URI("wss://your-server.com/todos"))
        
        val config = ReplicatorConfigurationFactory.create(
            database = database,
            target = endpoint,
            type = ReplicatorType.PUSH_AND_PULL,
            continuous = true,
            authenticator = BasicAuthenticator(username, "password")
        )
        
        replicator = Replicator(config).apply {
            addChangeListener { change ->
                when (change.status.activityLevel) {
                    ReplicatorActivityLevel.BUSY -> Log.d("Sync", "Syncing...")
                    ReplicatorActivityLevel.IDLE -> Log.d("Sync", "Up to date")
                    ReplicatorActivityLevel.OFFLINE -> Log.d("Sync", "Offline")
                    else -> {}
                }
            }
            start()
        }
    }
    
    fun stopSync() {
        replicator?.stop()
    }
}
```

That's it. Multi-device sync. Real-time. Conflict resolution included.

Compare that to the 800 lines of manual sync code I deleted.

### 2. Automatic Conflict Resolution

Remember my sync conflicts? Couchbase handles them automatically. If two devices edit different fields, it merges them. If they edit the same field, the most recent wins (by default).

You can customize it:

```kotlin
val config = ReplicatorConfigurationFactory.create(
    database = database,
    target = endpoint,
    conflictResolver = { conflict ->
        val local = conflict.localDocument
        val remote = conflict.remoteDocument
        
        // Custom logic: prefer completed todos
        if (local.getBoolean("isCompleted")) {
            local
        } else if (remote.getBoolean("isCompleted")) {
            remote
        } else {
            // Default: most recently updated
            if (local.getLong("updatedAt") > remote.getLong("updatedAt")) {
                local
            } else {
                remote
            }
        }
    }
)
```

But honestly? The default works great.

### 3. Real-Time Updates

The Flow from my repository automatically updates when any device makes a change. No polling. No manual refresh.

Edit a todo on your phone, it updates on your tablet instantly (if both are online).

### 4. Offline First (For Real)

With Room, I had to think about offline scenarios. Queue operations, handle failures, retry, etc.

With Couchbase, offline is the default. All operations are local. Sync happens in the background when connectivity is available.

User doesn't have to know or care about network state.

### 5. Flexible Schema

Need to add a "priority" field to todos?

Room:
```kotlin
// Create a migration
val MIGRATION_2_3 = object : Migration(2, 3) {
    override fun migrate(database: SupportSQLiteDatabase) {
        database.execSQL("ALTER TABLE todos ADD COLUMN priority INTEGER NOT NULL DEFAULT 0")
    }
}
```

Couchbase:
```kotlin
// Just save it
doc.setInt("priority", priority)
database.save(doc)
```

Old documents without priority? They just return 0 or whatever default you specify in your code.

## The Final Count

**Lines of Code Removed:**
- Room entities and DAOs: 120 lines
- Manual sync logic: 800 lines
- Conflict resolution: 150 lines
- Network monitoring: 80 lines
- Sync status tracking: 60 lines
- Total: 1,210 lines deleted

**Lines of Code Added:**
- Couchbase repository: 150 lines
- Sync setup: 40 lines
- Data migration: 30 lines
- Helper extensions: 40 lines
- Total: 260 lines added

**Net result: 950 lines of code deleted.**

My app got smaller and gained more features.

## Performance Comparison

I was worried about performance. Here's what I measured:

**Insert 100 todos:**
- Room: 45ms
- Couchbase: 52ms

**Query 1000 todos:**
- Room: 12ms
- Couchbase: 15ms

**Update 50 todos:**
- Room: 30ms
- Couchbase: 38ms

Couchbase is slightly slower for pure local operations. But when you factor in sync (which Room doesn't have), it's not even close.

## Real World Usage

After 3 months in production:
- 0 data loss incidents (had 3 with manual sync)
- 0 sync conflicts reported by users (had 12 with manual sync)
- 95% reduction in sync-related crashes
- Users actually use multiple devices now

## The Gotchas

Not everything was perfect:

### APK Size
Added about 4MB to my APK. For a todo app, that's noticeable. But users seem okay with it given the sync features.

### Learning Curve
Took me about a week to feel comfortable with Couchbase concepts. Documents, revisions, N1QL queries. It's different from Room.

### Backend Setup
You need Sync Gateway running on your server. It's not difficult, but it's one more thing to deploy and maintain.

Docker Compose for reference:

```yaml
version: '3'
services:
  sync-gateway:
    image: couchbase/sync-gateway:3.1.0
    ports:
      - "4984:4984"
    volumes:
      - ./sync-gateway-config.json:/etc/sync-gateway/config.json
```

### Debugging
Room's SQL queries in Logcat are easy to debug. Couchbase's replication protocol logs are... less intuitive.

## Would I Do It Again?

Absolutely.

If I was building a single-device app with no sync requirements, I'd probably still use Room. It's simpler and more Android-idiomatic.

But for anything that needs:
- Multi-device support
- Offline-first architecture
- Real-time collaboration
- Bulletproof sync

I'm using Couchbase from day one. The migration was painful, but doing sync manually was more painful.

## Advice If You're Considering It

1. **Try it in a side project first**: Don't migrate your production app without testing.
2. **Read the docs thoroughly**: Especially around replication and conflicts.
3. **Plan your document structure**: Unlike Room, schema changes are easier but less structured.
4. **Set up Sync Gateway early**: Don't wait until you need it.
5. **Write good tests**: The dynamic nature means you need test coverage.

## The Bottom Line

I deleted 950 lines of buggy sync code and replaced it with 40 lines that actually work.

My users can now seamlessly use the app across devices.

I sleep better knowing conflicts are handled automatically.

Was it worth migrating? Hell yes.

Would I recommend it? If you need sync, absolutely.

Do I miss Room? Not even a little bit.

---

*Have you migrated from Room to something else? How did it go? Or are you considering it? Let me know your thoughts.*
