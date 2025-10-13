# Exercise 2 Speaker Notes: Integrate Gemini API for LLM

**Transition:** `exercise1` → `excercise2`

**Duration:** ~20 minutes

---

## 🎯 Learning Objectives

Students will learn to:
- Configure Google Gemini API for optimal Q&A performance
- Make async API calls using Kotlin coroutines
- Understand LLM temperature, topP parameters
- Test external API integration
- Initialize database in Activity lifecycle

---

## 📋 Prerequisites Check

Before starting, ensure students have:
- ✅ Completed Exercise 1 (Database initialized)
- ✅ Obtained Gemini API key from Google AI Studio
- ✅ Added API key to `local.properties`
- ✅ App running without crashes

---

## 🔑 API Key Setup (5 min)

### Guide Students Through:

1. **Get API Key:**
   - Visit: https://makersuite.google.com/app/apikey
   - Click "Create API Key"
   - Copy the key

2. **Add to local.properties:**
```properties
# File: local.properties (root of project)
geminiKey="YOUR_ACTUAL_API_KEY_HERE"
```

**Important reminders:**
- ⚠️ Never commit this file to Git (already in `.gitignore`)
- ⚠️ Use quotes around the key
- ⚠️ No spaces around the `=`

3. **Verify Build Config:**
Show `app/build.gradle.kts` (lines 44-56):
```kotlin
buildTypes {
    release {
        buildConfigField("String", "geminiKey", "\"${geminiKey}\"")
    }
    debug {
        buildConfigField("String", "geminiKey", "\"${geminiKey}\"")
    }
}
```

**Explain:** "Gradle reads your `local.properties` and bakes the API key into `BuildConfig` at compile time."

---

## 🔄 What Changes in This Exercise

### Files Modified:
1. **`GeminiRemoteAPI.kt`** - Activate real Gemini integration
2. **`MainActivity.kt`** - Add database init + API test

### Key Concepts Introduced:
- LLM configuration (temperature, topP)
- Async network calls with `suspend` functions
- Coroutines and Dispatchers
- Lifecycle-aware initialization

---

## 📝 Step-by-Step Walkthrough

### Part 1: Understanding LLM Parameters (3 min)

**Opening question:** "Who knows what 'temperature' means in AI?"

**Explain with analogy:**
> "Imagine asking 'What's 2+2?' 
> - Temperature 0.0: Always answers '4' (deterministic)
> - Temperature 1.0: Might answer '4', 'four', '2+2=4', 'approximately 4' (creative)
> 
> For document Q&A, we want consistency, so we use low temperature."

---

### Part 2: Activate GeminiRemoteAPI.kt (8 min)

#### 2.1 Uncomment Real Implementation (Lines 1-36)

**Package & Imports (Lines 1-7):**
```kotlin
package com.ml.couchbase.docqa.domain.llm

import android.util.Log
import com.google.ai.client.generativeai.GenerativeModel
import com.google.ai.client.generativeai.type.GenerationConfig
import com.ml.couchbase.docqa.BuildConfig
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
```

**Point out:**
- `GenerativeModel` - Main Gemini SDK class
- `BuildConfig` - Where our API key lives
- `Dispatchers` - Thread pool management

---

#### 2.2 API Key Loading (Lines 10-12)
```kotlin
class GeminiRemoteAPI {
    private val apiKey = BuildConfig.geminiKey
    private val generativeModel: GenerativeModel
```

**Explain:**
> "`BuildConfig.geminiKey` reads from your build config. If this is empty, Gradle build will include an empty string - API calls will fail but app won't crash at startup."

**Demo:** Show where `BuildConfig` class is generated:
`app/build/generated/source/buildConfig/debug/com/ml/couchbase/docqa/BuildConfig.java`

---

#### 2.3 Model Configuration (Lines 14-27)

```kotlin
init {
    val configBuilder = GenerationConfig.Builder()
    configBuilder.topP = 0.4f
    configBuilder.temperature = 0.3f
    generativeModel = GenerativeModel(
        modelName = "gemini-2.5-flash",
        apiKey = apiKey,
        generationConfig = configBuilder.build()
    )
}
```

**Deep dive on each parameter:**

**Temperature (0.3):**
```
0.0 ──────────────────────────── 1.0
Deterministic              Creative
Factual                    Varied
Consistent                 Surprising
```
**Our choice: 0.3** - Slightly creative but mostly factual

**Demo:** Show what happens with different temperatures:
- Temperature 0.0: "Vector search uses embeddings."
- Temperature 1.0: "Ah, vector search! It's like having a magical library where..."

**topP (0.4):**
```
Also called "nucleus sampling"
```
**Visual explanation:**
```
All possible next tokens:
[the: 40%, a: 25%, this: 15%, that: 10%, an: 5%, ...]

topP = 0.4 means: Only consider tokens until cumulative prob > 40%
Selected pool: [the: 40%]  ← Only most likely token

topP = 0.8 means: Consider tokens until cumulative prob > 80%
Selected pool: [the: 40%, a: 25%, this: 15%]  ← More variety
```

**Our choice: 0.4** - Focus on most likely tokens (reduces hallucination)

**Model: gemini-2.5-flash**
- **Flash** = Fast, cheap, good for simple Q&A
- **Pro** = Slower, expensive, better for complex reasoning
- For document Q&A: Flash is sufficient!

**Cost comparison:**
- Flash: $0.075 per 1M input tokens
- Pro: $1.25 per 1M input tokens
- ~15x cheaper!

---

#### 2.4 The API Call Function (Lines 30-35)

```kotlin
suspend fun getResponse(prompt: String): String? =
    withContext(Dispatchers.IO) {
        Log.e("APP", "Prompt given: $prompt")
        val response = generativeModel.generateContent(prompt)
        return@withContext response.text
    }
```

**Break it down line by line:**

**`suspend fun`** - "Suspending function"
```kotlin
// This function can be paused and resumed
// Doesn't block the calling thread
// Must be called from a coroutine
```

**Ask students:** "Why not just regular function?"
- **Answer:** Network call takes time (500ms - 3s). UI would freeze!

**`withContext(Dispatchers.IO)`** - Thread switcher
```
UI Thread (Main) ──────┐
                       │ Switch to IO pool
                       ↓
IO Thread Pool ─────── Network call happens here
                       ↑
                       │ Switch back to caller's thread
UI Thread (Main) ──────┘ Result returned
```

**Dispatchers.IO:**
- Thread pool optimized for I/O operations
- Blocks for network/disk without affecting UI
- Managed by Kotlin runtime (we don't create threads manually)

**`generateContent(prompt)`:**
- Sends HTTP POST to Gemini API
- Includes your API key in headers
- Returns structured response

**`response.text`:**
- Extracts plain text from response
- Strips formatting, metadata
- Returns `String?` (nullable) if API fails

---

#### 2.5 Comment Out Mock Implementation (Lines 40-46)

**Show the difference:**
```kotlin
// OLD (Mock):
class GeminiRemoteAPI {
    suspend fun getResponse(prompt: String): String? {
        return "This is a mock response from Gemini API for prompt: $prompt"
    }
}

// NEW (Real):
class GeminiRemoteAPI {
    private val apiKey = BuildConfig.geminiKey
    private val generativeModel: GenerativeModel
    // ... actual API calls
}
```

---

### Part 3: Update MainActivity.kt (5 min)

#### 3.1 Add Imports (Lines 42-46)
```kotlin
import android.util.Log
import androidx.lifecycle.lifecycleScope
import com.ml.couchbase.docqa.data.DatabaseManager
import com.ml.couchbase.docqa.domain.llm.GeminiRemoteAPI
import kotlinx.coroutines.launch
```

**Explain:**
- `lifecycleScope` - Coroutine scope tied to Activity lifecycle
- If Activity is destroyed, coroutines are cancelled automatically

---

#### 3.2 Initialize Database (Lines 65-66)
```kotlin
override fun onCreate(savedInstanceState: Bundle?) {
    super.onCreate(savedInstanceState)
    enableEdgeToEdge()
    DatabaseManager.init(applicationContext)  // ← NEW!
    testGeminiAPI()  // ← NEW!
    setContent {
        // ... UI code
    }
}
```

**Important teaching moment:**
> "Why in `onCreate()`?"
> - Runs once when Activity first created
> - Before any UI is shown
> - Guarantees database ready before user interaction

**Why `applicationContext`?**
```kotlin
// WRONG:
DatabaseManager.init(this)  // 'this' = Activity context

// RIGHT:
DatabaseManager.init(applicationContext)  // App-level context
```
**Diagram:**
```
Application Context
    └── Activity Context (created/destroyed on rotation)
    └── Activity Context (created/destroyed on rotation)
    └── ...

Database lives at Application level!
```

---

#### 3.3 Test Function (Lines 84-90)
```kotlin
private fun testGeminiAPI() {
    val geminiAPI = GeminiRemoteAPI()
    lifecycleScope.launch {
        val response = geminiAPI.getResponse("What is the capital of France?")
        Log.d("GeminiTest", "Response: $response")
    }
}
```

**Walk through execution:**

1. **`val geminiAPI = GeminiRemoteAPI()`**
   - Creates instance
   - Loads API key from BuildConfig
   - Configures model

2. **`lifecycleScope.launch { ... }`**
   - Creates new coroutine
   - Tied to Activity lifecycle
   - Runs on Main thread by default

3. **`getResponse("What is the capital of France?")`**
   - `suspend` function, so coroutine pauses here
   - Switches to IO thread for network call
   - Waits for API response
   - Returns to Main thread with result

4. **`Log.d("GeminiTest", "Response: $response")`**
   - Should print: "Response: Paris"
   - Or: "Response: The capital of France is Paris."

---

### Part 4: Build, Run & Verify (4 min)

**Guided testing:**

1. **Sync Gradle** (may take a minute to download Gemini SDK)

2. **Run app**

3. **Open Logcat** and filter by "GeminiTest"

**Expected output:**
```
D/GeminiTest: Response: The capital of France is Paris.
```

**If you see this: ✅ Success!**

---

## 🐛 Troubleshooting Guide

### Issue 1: Empty Response
```
D/GeminiTest: Response: null
```

**Likely causes:**
1. API key not in `local.properties`
2. API key incorrect/expired
3. No internet connection
4. Gemini API quota exceeded

**Fix:**
```kotlin
// Add better error handling:
private fun testGeminiAPI() {
    val geminiAPI = GeminiRemoteAPI()
    lifecycleScope.launch {
        try {
            val response = geminiAPI.getResponse("What is the capital of France?")
            Log.d("GeminiTest", "Response: $response")
        } catch (e: Exception) {
            Log.e("GeminiTest", "Error: ${e.message}", e)
        }
    }
}
```

### Issue 2: Build Error - "Unresolved reference: geminiKey"
```
BuildConfig.geminiKey
            ^
```

**Cause:** `local.properties` doesn't have the key

**Fix:**
1. Create/check `local.properties` in project root
2. Add: `geminiKey="your_key_here"`
3. Rebuild project (Build → Rebuild Project)

### Issue 3: Network Error
```
E/GeminiTest: Error: Unable to resolve host "generativelanguage.googleapis.com"
```

**Cause:** No internet / Emulator networking issue

**Fix:**
- Check WiFi
- Try on real device
- Cold boot emulator (AVD Manager → Cold Boot Now)

---

## 💡 Interactive Discussion Points

### Q1: "Why not use Gemini for embedding generation?"
**Answer:** 
- Gemini API is for text generation (expensive, slow)
- Embeddings need to be generated for EVERY chunk (could be 1000s)
- Local ONNX model is free and instant
- Best practice: Local embeddings + Cloud LLM

### Q2: "Can we use other LLMs like GPT-4?"
**Answer:** Yes! Just replace GeminiRemoteAPI with:
```kotlin
class OpenAIRemoteAPI {
    suspend fun getResponse(prompt: String): String? {
        // Call OpenAI API instead
    }
}
```
Same pattern, different endpoint!

### Q3: "What if API is down during user session?"
**Answer:** (Preview Exercise 4)
- We'll add error handling
- Show user-friendly error message
- Maybe implement retry logic
- This is why we return `String?` (nullable)

---

## 🎓 Key Concepts to Emphasize

### 1. Coroutines vs Threads
```kotlin
// OLD WAY (manual threading):
Thread {
    val response = api.call()
    runOnUiThread {
        updateUI(response)
    }
}.start()

// NEW WAY (coroutines):
lifecycleScope.launch {
    val response = api.getResponse()  // Suspends, doesn't block
    updateUI(response)  // Already on Main thread
}
```

**Benefits:**
- Less boilerplate
- Automatic thread management
- Lifecycle-aware cancellation
- Better error handling

### 2. suspend Functions
```kotlin
suspend fun getResponse(prompt: String): String?
```
**Rules:**
- Can only be called from coroutines or other suspend functions
- Can use other suspend functions inside
- Compiler transforms code to state machine (not blocking!)

### 3. Context Switching
```kotlin
withContext(Dispatchers.IO) {
    // This code runs on IO thread pool
}
// Automatically returns to previous dispatcher
```

**Available Dispatchers:**
- **Main** - UI updates (Android main thread)
- **IO** - Network, database, file operations
- **Default** - CPU-intensive work (computation)

---

## 🎯 Success Criteria

Students have successfully completed Exercise 2 when:

✅ API key configured in `local.properties`  
✅ Build succeeds without errors  
✅ App launches and shows UI  
✅ Logcat shows database initialization messages  
✅ Logcat shows Gemini response: "Paris" or similar  
✅ No crashes or API errors

**Bonus check:**
```
I/DatabaseManager: CouchbaseLite initialized successfully
I/DatabaseManager: Vector Search enabled successfully
I/DatabaseManager: Database 'myDatabase' opened successfully
D/GeminiTest: Response: The capital of France is Paris.
```

---

## 🚀 Transition to Exercise 3

**Closing remarks:**
> "Excellent! We now have both database initialization AND AI integration working. But they're not connected yet. In Exercise 3, we'll implement the document storage layer - allowing users to upload PDFs/DOCX files, extract text, generate embeddings, and store everything in Couchbase."

**Preview Exercise 3:**
- Store full documents with metadata
- Generate embeddings using local ONNX model
- Split documents into chunks
- Save chunks with their embeddings
- Implement CRUD operations

**Challenge (optional):**
"Try changing the test question to ask about Couchbase. Does Gemini know about it without context? This motivates why we need RAG!"

Example:
```kotlin
val response = geminiAPI.getResponse(
    "How do I create a vector index in Couchbase Lite?"
)
// Response might be: "I don't have specific information about that."
// This shows why we need to provide context from documents!
```

---

## 📚 Additional Resources

- [Gemini API Documentation](https://ai.google.dev/docs)
- [Kotlin Coroutines Guide](https://kotlinlang.org/docs/coroutines-guide.html)
- [Android Coroutines Best Practices](https://developer.android.com/kotlin/coroutines)
- [LLM Parameters Explained](https://ivibudh.medium.com/a-guide-to-controlling-llm-model-output-exploring-top-k-top-p-and-temperature-parameters-ed6a31313910)

---

## ⏱️ Time Management

- API key setup: 5 min
- GeminiRemoteAPI explanation: 8 min
- MainActivity updates: 5 min
- Testing & verification: 4 min
- Q&A: 3 min
- **Total: ~25 min** (can compress to 20 if needed)

---

**Next File:** `Exercise3_SpeakerNotes.md`

