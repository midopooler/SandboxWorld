# Exercise 1 Speaker Notes: Initialize Couchbase Lite Database

**Transition:** `main` → `exercise1`

**Duration:** ~15 minutes

---

## 🎯 Learning Objectives

Students will learn to:
- Initialize Couchbase Lite SDK in an Android app
- Enable the Vector Search extension
- Handle initialization errors properly
- Understand the difference between mock and real implementations

---

## 📋 Prerequisites Check

Before starting, ensure students have:
- ✅ Android Studio open with project loaded
- ✅ Checked out `main` branch
- ✅ Ran Gradle sync successfully
- ✅ Reviewed the placeholder code structure

---

## 🔄 What Changes in This Exercise

### Files Modified:
1. **`DatabaseManager.kt`** - Swap mock → real implementation
2. **Sample PDFs removed** - Clean up boilerplate files

### Key Change Summary:
- **Before:** Placeholder `MockDatabase` that does nothing
- **After:** Real Couchbase Lite with Vector Search enabled

---

## 📝 Step-by-Step Walkthrough

### Part 1: Explain the Problem (2 min)

**Talking Points:**
> "Right now, our app compiles but doesn't actually do anything. All database operations are mock placeholders. Let's change that."

Show the current `DatabaseManager.kt`:
```kotlin
// Lines 46-61: Mock implementation (currently active)
object DatabaseManager {
    fun init(context: Context, dbName: String = "myDatabase") {
        Log.i(TAG, "Placeholder: Database '$dbName' initialized successfully")
    }
    fun getDatabase(): MockDatabase = MockDatabase()
    fun isVectorSearchEnabled(): Boolean = true  // Always true (fake)
}
```

**Key Point:** "Notice it just logs a message and returns fake objects. No real database!"

---

### Part 2: The Real Implementation (5 min)

**Uncomment lines 1-42 (Real Implementation)**

#### 2.1 Package & Imports (Lines 1-5)
```kotlin
package com.ml.couchbase.docqa.data

import android.content.Context
import android.util.Log
import com.couchbase.lite.*
```

**Say:** "First, we import the real Couchbase SDK classes. Note the `com.couchbase.lite.*` import."

---

#### 2.2 Database Instance (Lines 9-11)
```kotlin
private lateinit var database: Database
private const val TAG = "DatabaseManager"
private var isVectorSearchEnabled = false
```

**Explain:**
- **`lateinit var database: Database`** - Real Couchbase database type (not Mock!)
- **`isVectorSearchEnabled`** - Tracks if vector extension loaded successfully
  - Important: Not all devices may support it
  - We check and set this flag properly

---

#### 2.3 Initialization Logic (Lines 13-30)
```kotlin
fun init(context: Context, dbName: String = "myDatabase") {
    try {
        CouchbaseLite.init(context)  // ← CRITICAL: Must be first!
        Log.i(TAG, "CouchbaseLite initialized successfully")
```

**Stop and Explain:**
> "This `CouchbaseLite.init(context)` MUST be called before ANY database operations. It sets up internal resources, file paths, and native libraries."

**Why context?** 
- Needs file system access for database storage
- Uses app's private storage: `/data/data/com.ml.couchbase.docqa/files/`

---

#### 2.4 Vector Search Extension (Lines 19-26)
```kotlin
try {
    CouchbaseLite.enableVectorSearch()
    isVectorSearchEnabled = true
    Log.i(TAG, "Vector Search enabled successfully")
} catch (e: CouchbaseLiteException) {
    Log.e(TAG, "Failed to enable Vector Search: ${e.message}")
    isVectorSearchEnabled = false
}
```

**Key Teaching Moment:**
> "Vector Search is a separate native library (C++). We try to load it, but if it fails, we gracefully handle it."

**Ask students:** "Why use try-catch here?"
- **Answer:** Some devices/emulators might not support the native library
- App should still work (without vector features) rather than crash

---

#### 2.5 Create Database (Lines 28-29)
```kotlin
database = Database(dbName)
Log.i(TAG, "Database '$dbName' opened successfully")
```

**Explain:**
- `Database(dbName)` creates OR opens existing database
- If `myDatabase.cblite2` exists → opens it
- If not → creates new empty database
- Database file location: Check in Logcat!

**Demo:** Show where to find the log in Android Studio

---

#### 2.6 Error Handling (Lines 33-36)
```kotlin
} catch (e: CouchbaseLiteException) {
    Log.e(TAG, "Failed to initialize database: ${e.message}")
    throw RuntimeException("Database initialization failed", e)
}
```

**Say:** "If initialization fails, we crash the app intentionally. Why?"
- **Answer:** Database is critical - app can't function without it
- Better to fail fast than have mysterious errors later

---

### Part 3: Comment Out Mock Code (3 min)

**Comment lines 46-105 (Mock implementation)**

**Say:** 
> "Now we comment out the placeholder code. In a real project, you'd delete it. But for learning, we keep it commented so you can see what we replaced."

**Show side-by-side:**
```kotlin
// OLD (Mock):
fun getDatabase(): MockDatabase = MockDatabase()

// NEW (Real):
fun getDatabase(): Database = database
```

---

### Part 4: Build & Run (3 min)

**Guided Activity:**

1. **Sync Gradle** 
   - File → Sync Project with Gradle Files
   
2. **Run the app**
   - Click Run (green triangle)
   - Watch Logcat

3. **What to look for:**
```
I/DatabaseManager: CouchbaseLite initialized successfully
I/DatabaseManager: Vector Search enabled successfully
I/DatabaseManager: Database 'myDatabase' opened successfully
```

**Troubleshooting Common Issues:**

| Error | Cause | Fix |
|-------|-------|-----|
| "CouchbaseLite not initialized" | Init not called | Check MainActivity calls `DatabaseManager.init()` |
| "Vector Search failed" | Emulator issue | OK! Flag is false, app continues |
| Build error | Couchbase dependency | Check `build.gradle.kts` has Couchbase libs |

---

## 🎓 Key Concepts to Emphasize

### 1. Initialization Order Matters
```kotlin
// CORRECT:
CouchbaseLite.init(context)  // First!
CouchbaseLite.enableVectorSearch()  // Second
database = Database(name)  // Third

// WRONG:
database = Database(name)  // Crashes! SDK not initialized
```

### 2. Object Singleton Pattern
```kotlin
object DatabaseManager { ... }
```
- **Only one instance** for entire app
- Thread-safe (Kotlin guarantees it)
- Database connection is expensive - share it!

### 3. lateinit vs val
```kotlin
private lateinit var database: Database  // Initialized later in init()
private const val TAG = "DatabaseManager"  // Known at compile time
```

---

## 💡 Interactive Discussion Points

### Q1: "Why not initialize in Application.onCreate()?"
**Answer:** We do! (Check `DocQAApplication` or `MainActivity.onCreate()`)
- Ensures database ready before any UI shows
- One-time setup at app launch

### Q2: "What if user has 1000 documents?"
**Answer:** Database file grows, but:
- Couchbase is optimized for mobile (lightweight)
- Lazy loading: Only requested data loads into memory
- Typical app: 100MB database runs smoothly

### Q3: "Can we have multiple databases?"
**Answer:** Yes!
```kotlin
val docsDB = Database("documents")
val settingsDB = Database("settings")
```
- Use separate DBs for different data domains
- This app uses one database with different document types

---

## 🐛 Common Student Mistakes

### Mistake 1: Not calling init()
```kotlin
// MainActivity - Students forget this:
override fun onCreate(savedInstanceState: Bundle?) {
    super.onCreate(savedInstanceState)
    DatabaseManager.init(applicationContext)  // ← Must have!
    // ...
}
```

### Mistake 2: Using Activity context
```kotlin
// WRONG:
DatabaseManager.init(this)  // Activity context

// CORRECT:
DatabaseManager.init(applicationContext)  // App context
```
**Why?** Activity context gets destroyed on rotation. Database outlives activities!

### Mistake 3: Uncommenting only part of code
- Students might uncomment real code but forget to comment out mock
- Result: Compilation errors from duplicate classes

**Solution:** Show them to swap the entire blocks systematically

---

## 🎯 Success Criteria

Students have successfully completed Exercise 1 when:

✅ App builds without errors  
✅ Logcat shows three success messages:
   - "CouchbaseLite initialized successfully"
   - "Vector Search enabled successfully" (or failed message is OK)
   - "Database 'myDatabase' opened successfully"  
✅ App launches and shows UI (even if still non-functional)  
✅ No crashes on startup

---

## 🚀 Transition to Exercise 2

**Closing remarks:**
> "Great! We now have a real database initialized. But it's empty and we're not using it yet. In Exercise 2, we'll connect to Google's Gemini API to enable AI-powered question answering. This will teach you how to integrate external LLM APIs with proper async handling."

**Preview:**
- Configure Gemini API with optimal parameters
- Make async network calls with Kotlin coroutines
- Test the integration with a simple query

**Homework (optional):**
"Try changing the database name from 'myDatabase' to 'workshopDB' and observe in Logcat that a new database file is created."

---

## 📚 Additional Resources

- [Couchbase Lite Android Documentation](https://docs.couchbase.com/couchbase-lite/current/android/gs-install.html)
- [Vector Search Extension Guide](https://docs.couchbase.com/couchbase-lite/current/android/vector-search.html)
- [Kotlin Object Declarations](https://kotlinlang.org/docs/object-declarations.html#object-declarations-overview)

---

## ⏱️ Time Management

- Explanation: 5 min
- Coding together: 5 min
- Build & verify: 3 min
- Q&A: 2 min
- **Total: ~15 min**

---

**Next File:** `Exercise2_SpeakerNotes.md`

