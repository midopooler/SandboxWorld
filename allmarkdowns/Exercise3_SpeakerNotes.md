# Exercise 3 Speaker Notes: Implement Document Storage & Embeddings

**Transition:** `excercise2` → `excercise3`

**Duration:** ~25 minutes

---

## 🎯 Learning Objectives

Students will learn to:
- Store documents in Couchbase with metadata
- Query documents using QueryBuilder API
- Generate embeddings using local ONNX models
- Store high-dimensional vectors in arrays
- Implement CRUD operations for documents and chunks
- Use Kotlin Flows for reactive data

---

## 📋 Prerequisites Check

Before starting, ensure students have:
- ✅ Completed Exercise 2 (Gemini API working)
- ✅ Database initialized successfully
- ✅ Understanding of embeddings concept (brief review if needed)
- ✅ ONNX model file present: `app/src/main/assets/all-MiniLM-L6-V2.onnx`

---

## 🧠 Brief Embeddings Review (3 min)

**Before diving into code, ensure students understand embeddings:**

**Ask:** "What's an embedding?"

**Visual explanation:**
```
Text: "Vector search is powerful"
  ↓ (Embedding Model)
Vector: [0.23, -0.15, 0.89, 0.45, ...(380 more values)]

Text: "Semantic search using vectors"
  ↓ (Same Model)
Vector: [0.21, -0.18, 0.91, 0.42, ...(380 more values)]

Distance between vectors = Semantic similarity!
Close vectors = Similar meaning
```

**Key points:**
- Embeddings capture semantic meaning
- Similar text → similar vectors
- We use **all-MiniLM-L6-V2** model (384 dimensions)
- Runs locally (no API calls needed!)

---

## 🔄 What Changes in This Exercise

### Files Modified:
1. **`DocumentsDB.kt`** - Activate document storage
2. **`ChunksDB.kt`** - Activate chunk storage (NOT vector search yet)
3. **`ChunksUseCase.kt`** - Activate embedding generation

### What's NOT Activated Yet:
- ❌ Vector index creation (Exercise 4)
- ❌ Vector similarity search (Exercise 4)
- ❌ Complete RAG pipeline (Exercise 4)

**Important:** We're storing chunks WITH embeddings, but not searching them yet!

---

## 📝 Step-by-Step Walkthrough

### Part 1: DocumentsDB.kt - Document Management (8 min)

#### 1.1 Remove Old Commented Code (Lines 1-58)
**Say:** "First, let's clean up. Delete all the old commented placeholder code at the top."

---

#### 1.2 Activate Real Implementation (Lines 3-59)

**Class Declaration (Lines 11-13):**
```kotlin
class DocumentsDB {
    private val database: Database = DatabaseManager.getDatabase()
```

**Explain:** "Gets the real Couchbase database instance we initialized in Exercise 1."

---

#### 1.3 addDocument() Function (Lines 15-22)

```kotlin
fun addDocument(document: Document): String {
    val mutableDoc = MutableDocument()
    mutableDoc.setString("docFileName", document.docFileName)
    mutableDoc.setString("docText", document.docText)
    mutableDoc.setLong("docAddedTime", document.docAddedTime)
    database.save(mutableDoc)
    return mutableDoc.id
}
```

**Walk through each line:**

**Line 16:** `val mutableDoc = MutableDocument()`
- Creates new document with auto-generated UUID
- Example ID: `"1a2b3c4d-5e6f-7g8h-9i0j-k1l2m3n4o5p6"`

**Line 17:** `setString("docFileName", document.docFileName)`
- Stores original filename
- Example: `"Couchbase Shell Documentation.pdf"`

**Line 18:** `setString("docText", document.docText)`
- Complete extracted text (can be huge!)
- Example: `"Chapter 1: Introduction\nCouchbase is a NoSQL database..."`
- This is what gets split into chunks later

**Line 19:** `setLong("docAddedTime", document.docAddedTime)`
- Timestamp in milliseconds
- Used for "Recently Added" sorting

**Line 20:** `database.save(mutableDoc)`
- Writes to disk immediately
- ACID transaction (atomic)

**Line 21:** `return mutableDoc.id`
- **Critical!** This ID links chunks to their parent document
- Used in next step when saving chunks

**Show the workflow:**
```kotlin
// 1. User uploads "sample.pdf"
val text = PDFReader.extract("sample.pdf")  // "Full text here..."

// 2. Save document
val docId = documentsDB.addDocument(
    Document(
        docFileName = "sample.pdf",
        docText = text,
        docAddedTime = System.currentTimeMillis()
    )
)
// Returns: "abc-123-def-456"

// 3. Use returned ID for chunks
chunks.forEach { chunk ->
    chunksDB.addChunk(docId = docId, ...)  // Link to parent!
}
```

---

#### 1.4 removeDocument() Function (Lines 24-28)

```kotlin
fun removeDocument(docId: String) {
    database.getDocument(docId)?.let {
        database.delete(it)
    }
}
```

**Explain null safety:**
```kotlin
// Without null safety (crashes if not found):
val doc = database.getDocument(docId)
database.delete(doc)  // ☠️ NullPointerException if doc doesn't exist

// With null safety (graceful):
database.getDocument(docId)?.let {
    database.delete(it)  // ✅ Only runs if document exists
}
```

**Important note:** "This only deletes the document, NOT its chunks! We'll need to delete chunks separately."

---

#### 1.5 getAllDocuments() Function (Lines 30-47)

```kotlin
@OptIn(ExperimentalCoroutinesApi::class)
fun getAllDocuments(): Flow<List<Document>> = flow {
    val query = QueryBuilder
        .select(SelectResult.all())
        .from(DataSource.database(database))
    
    val result = query.execute()
    val documents = result.map { row ->
        val doc = row.getDictionary(database.name)
        Document(
            docId = doc?.getString("id")?.toLong() ?: 0,
            docFileName = doc?.getString("docFileName") ?: "",
            docText = doc?.getString("docText") ?: "",
            docAddedTime = doc?.getLong("docAddedTime") ?: 0
        )
    }
    emit(documents)
}.flowOn(Dispatchers.IO)
```

**LINE-BY-LINE BREAKDOWN:**

**Line 1:** `@OptIn(ExperimentalCoroutinesApi::class)`
- Acknowledges using experimental Kotlin Flow API
- Required by compiler when using certain Flow features
- Will be removed when Flow becomes stable

**Line 2:** `fun getAllDocuments(): Flow<List<Document>> = flow {`
- **Return type:** `Flow<List<Document>>` - A stream that emits lists of documents
- **`= flow { ... }`** - Builder function that creates the Flow
- Think of Flow as a pipe that data flows through asynchronously

**Lines 3-5:** Building the Query
```kotlin
val query = QueryBuilder                            // Start building a query
    .select(SelectResult.all())                     // SELECT * (all fields)
    .from(DataSource.database(database))            // FROM myDatabase
```
- **`QueryBuilder`** - Type-safe SQL query builder (prevents SQL injection)
- **`SelectResult.all()`** - Selects all fields from documents
- **`DataSource.database(database)`** - Specifies which database to query
- **SQL equivalent:** `SELECT * FROM myDatabase`

**Line 7:** `val result = query.execute()`
- Executes the query against the database
- Returns a `ResultSet` - iterable collection of rows
- This is where actual I/O happens (reads from disk)

**Lines 8-15:** Transforming Results
```kotlin
val documents = result.map { row ->                 // For each row in results
    val doc = row.getDictionary(database.name)      // Extract document dictionary
    Document(                                        // Create Kotlin Document object
        docId = doc?.getString("id")?.toLong() ?: 0,
        docFileName = doc?.getString("docFileName") ?: "",
        docText = doc?.getString("docText") ?: "",
        docAddedTime = doc?.getLong("docAddedTime") ?: 0
    )
}
```

**Line 9 explained:** `val doc = row.getDictionary(database.name)`
- **`row`** - Single result row (one document)
- **`.getDictionary(database.name)`** - Extracts the document data as key-value map
- **`database.name`** - Keys the result by database name (e.g., "myDatabase")
- Returns `Dictionary?` (nullable) - might be null if row is malformed

**Lines 11-14 explained:** Field extraction with null safety
```kotlin
docId = doc?.getString("id")?.toLong() ?: 0
       ^^^^ ^^^^^^^^^^^^^^^^^  ^^^^^^^^  ^^
        |         |                |       |
        |         |                |       Default if null
        |         |                Convert String to Long
        |         Get "id" field as String (returns null if missing)
        Null-safe operator (if doc is null, entire chain returns null)
```

**Each field extraction chain:**
1. `doc?.getString("id")` - Get field, returns `String?` (nullable)
2. `?.toLong()` - Convert to Long, returns `Long?` (nullable)
3. `?: 0` - Elvis operator: use 0 if previous result was null

**Why this pattern?**
- Database might not have the field (old schema)
- Field might be null
- Prevents `NullPointerException` crashes

**Line 16:** `emit(documents)`
- **`emit()`** - Pushes the list of documents into the Flow
- Like saying "Here's the data!" to anyone listening
- Triggers all collectors to receive this data

**Line 17:** `.flowOn(Dispatchers.IO)`
- **Changes the dispatcher** (thread pool) for the flow
- **`Dispatchers.IO`** - Thread pool optimized for I/O operations
- Everything above this line runs on IO thread
- Results are delivered back to caller's thread
- **Critical:** Keeps UI thread responsive during database operations

**Flow Concept Analogy:**
```
Think of Flow as a water pipe:

Database ─────┐
              │ flow { ... }  ← Water source
              ↓
        [Processing]          ← Filtering/transforming
              ↓
        .flowOn(IO)           ← Flow through IO pipe
              ↓
         .collect { }         ← Water comes out here (in UI)
              ↓
        Update UI             ← Use the water
```

**Usage in ViewModel:**
```kotlin
// In ViewModel
val documents: StateFlow<List<Document>> = 
    documentsDB.getAllDocuments()
        .stateIn(viewModelScope, SharingStarted.Lazily, emptyList())
        
// In Composable UI
val docs by viewModel.documents.collectAsState()
LazyColumn {
    items(docs) { doc ->
        Text(doc.docFileName)
    }
}
// UI automatically updates when database changes!
```

**Flow vs Callback comparison:**
```kotlin
// OLD WAY (Callback Hell):
database.getDocuments(object : Callback {
    override fun onSuccess(docs: List<Document>) {
        runOnUiThread { updateUI(docs) }
    }
    override fun onError(e: Exception) {
        runOnUiThread { showError(e) }
    }
})

// NEW WAY (Flow):
documentsDB.getAllDocuments()
    .collect { docs -> updateUI(docs) }
    // Errors handled by try-catch in collector
    // Thread switching automatic
```

---

#### 1.6 getDocsCount() Function (Lines 49-56)

```kotlin
fun getDocsCount(): Long {
    val query = QueryBuilder
        .select(SelectResult.expression(Function.count(Expression.string("*"))))
        .from(DataSource.database(database))
    
    val result = query.execute().firstOrNull()?.getInt(0)
    return result?.toLong() ?: 0L
}
```

**SQL equivalent:**
```sql
SELECT COUNT(*) FROM myDatabase
```

**Usage in UI:**
```kotlin
if (documentsDB.getDocsCount() == 0) {
    showEmptyState()  // "Upload documents to get started"
} else {
    enableChatInput()  // Ready for questions!
}
```

---

### Part 2: ChunksDB.kt - Chunk Storage (8 min)

#### 2.1 Activate Imports (Lines 1-5)

```kotlin
import com.couchbase.lite.*
import com.couchbase.lite.VectorEncoding
import com.couchbase.lite.VectorIndexConfiguration
```

**Note:** "We're importing vector classes, but NOT using them yet in this exercise."

---

#### 2.2 Keep Vector Index COMMENTED (Lines 11-21)

```kotlin
// init {
//     createVectorIndex()
// }
// 
// private fun createVectorIndex() {
//     val config = VectorIndexConfiguration("chunkEmbedding", 384, 3)
//     config.encoding = VectorEncoding.none()
//     database.createIndex(INDEX_NAME, config)
// }
```

**Emphasize:** "Leave this commented! We'll activate it in Exercise 4."

**Why wait?**
- Students need to understand basic storage first
- Index creation is complex and can fail
- Better to test storage works before adding indexing

---

#### 2.3 Activate addChunk() (Lines 23-31)

```kotlin
fun addChunk(chunk: Chunk) {
    val mutableDoc = MutableDocument()
    mutableDoc.setString("docFileName", chunk.docFileName)
    mutableDoc.setString("chunkData", chunk.chunkData)
    mutableDoc.setArray("chunkEmbedding", MutableArray().apply {
        chunk.chunkEmbedding.forEach { addFloat(it) }
    })
    database.save(mutableDoc)
}
```

**Focus on the embedding storage (Lines 28-30):**

```kotlin
mutableDoc.setArray("chunkEmbedding", MutableArray().apply {
    chunk.chunkEmbedding.forEach { addFloat(it) }
})
```

**Visual explanation:**
```kotlin
// Input: chunk.chunkEmbedding (Kotlin FloatArray)
floatArrayOf(0.123f, -0.456f, 0.789f, ...(381 more))

// Conversion process:
MutableArray()
    .addFloat(0.123)
    .addFloat(-0.456)
    .addFloat(0.789)
    // ... 381 more times

// Output: MutableArray (Couchbase type)
[0.123, -0.456, 0.789, ...(381 more)]
```

**Storage size calculation:**
```
Text chunk: ~500 bytes
Embedding: 384 floats × 4 bytes = 1,536 bytes
Metadata: ~100 bytes
Total: ~2 KB per chunk

1000 chunks ≈ 2 MB
10,000 chunks ≈ 20 MB
```

**Resulting document:**
```json
{
  "_id": "chunk_abc123",
  "docFileName": "sample.pdf",
  "chunkData": "Vector search enables semantic similarity...",
  "chunkEmbedding": [0.123, -0.456, 0.789, ...(381 more values)]
}
```

---

#### 2.4 Keep getSimilarChunks() COMMENTED (Lines 33-76)

```kotlin
// fun getSimilarChunks(queryEmbedding: FloatArray, n: Int = 5): ...
```

**Say:** "This is the vector search function. Keep it commented for now - Exercise 4!"

---

#### 2.5 Activate removeChunks() (Lines 80-92)

```kotlin
fun removeChunks(docId: Long) {
    val query = QueryBuilder
        .select(SelectResult.expression(Meta.id))
        .from(DataSource.database(database))
        .where(Expression.property("docId").equalTo(Expression.longValue(docId)))
    
    val result = query.execute()
    result.forEach { row ->
        val docId = row.getString("id") ?: return@forEach
        val doc = database.getDocument(docId)
        doc?.let { database.delete(it) }
    }
}
```

**SQL equivalent:**
```sql
SELECT META().id 
FROM myDatabase 
WHERE docId = ?
```

**Walk through deletion:**
1. Query finds all chunks with matching `docId`
2. For each chunk, load the document
3. Delete it
4. Index will auto-update (in Exercise 4)

**Usage example:**
```kotlin
// User deletes "sample.pdf" (docId = 42)
chunksDB.removeChunks(42)  // Deletes ALL chunks from that document
documentsDB.removeDocument("doc_abc")  // Then delete the document
```

---

#### 2.6 Comment Out Placeholder (Lines 96-117)

**Show the contrast:**
```kotlin
// OLD (Placeholder):
fun addChunk(chunk: Chunk) {
    // Placeholder implementation - does nothing!
}

// NEW (Real):
fun addChunk(chunk: Chunk) {
    val mutableDoc = MutableDocument()
    // ... actually saves to database
    database.save(mutableDoc)
}
```

---

### Part 3: ChunksUseCase.kt - Embedding Generation (7 min)

#### 3.1 Activate Real Implementation (Lines 1-46)

**Key change: NEW dependency!**

```kotlin
// OLD constructor (Exercise 2):
constructor(private val chunksDB: ChunksDB)

// NEW constructor (Exercise 3):
constructor(
    private val chunksDB: ChunksDB,
    private val sentenceEncoder: SentenceEmbeddingProvider  // ← NEW!
)
```

**Explain SentenceEmbeddingProvider:**
> "This is a wrapper around the ONNX model. It loads the `all-MiniLM-L6-V2.onnx` file from assets and provides a simple `encodeText()` function."

---

#### 3.2 addChunk() with Embeddings (Lines 15-26)

```kotlin
fun addChunk(docId: String, docFileName: String, chunkText: String) {
    val embedding = sentenceEncoder.encodeText(chunkText)
    Log.e("APP", "Embedding dims ${embedding.size}")
    chunksDB.addChunk(
        Chunk(
            docId = docId,
            docFileName = docFileName,
            chunkData = chunkText,
            chunkEmbedding = embedding
        )
    )
}
```

**Line 16: The magic happens here!**
```kotlin
val embedding = sentenceEncoder.encodeText(chunkText)
```

**Demo with example:**
```kotlin
// Input text:
val text = "Vector search enables semantic similarity matching"

// ONNX model processing:
// 1. Tokenize: ["vector", "search", "enables", ...]
// 2. Convert to IDs: [4832, 3921, 8472, ...]
// 3. Run through neural network
// 4. Produce 384 dimensions

// Output:
val embedding = [0.023, -0.156, 0.789, ...(381 more)]

// Verify:
Log.e("APP", "Embedding dims ${embedding.size}")
// Output: "Embedding dims 384" ✓
```

**Performance:**
- Small chunks (~100 words): ~10-20ms
- Large chunks (~500 words): ~30-50ms
- Runs on device CPU (no network!)

---

#### 3.3 Keep getSimilarChunks() COMMENTED (Lines 32-35)

```kotlin
// fun getSimilarChunks(query: String, n: Int = 5): ...
```

**Explain:** "We're storing embeddings but not searching them yet. Exercise 4 activates this!"

---

#### 3.4 Comment Out Placeholder (Lines 48-72)

**Show what we replaced:**
```kotlin
// OLD:
fun addChunk(docId: String, docFileName: String, chunkText: String) {
    // Placeholder - doesn't generate embeddings
}

// NEW:
fun addChunk(docId: String, docFileName: String, chunkText: String) {
    val embedding = sentenceEncoder.encodeText(chunkText)  // Generate!
    chunksDB.addChunk(Chunk(..., chunkEmbedding = embedding))  // Save!
}
```

---

### Part 4: Testing Strategy (2 min)

**Note:** We can't fully test this yet because UI for document upload isn't exposed in this exercise.

**What we CAN verify:**
1. App compiles ✓
2. Database initialization still works ✓
3. Gemini test still works ✓
4. No crashes on startup ✓

**What we'll test in Exercise 4:**
- Upload a document
- See chunks being created
- Run vector search

**For now: Build and run!**

---

## 🎓 Key Concepts to Emphasize

### 1. Document vs Chunk Relationship

```
Document (DocumentsDB)
├── Full text: "Chapter 1...(10,000 words)"
├── Metadata: filename, timestamp
└── ID: "doc_abc123"

Chunks (ChunksDB) ──────────┐
├── Chunk 1                  │
│   ├── docId: "doc_abc123" ←┘ (Reference to parent)
│   ├── text: "Chapter 1..."
│   └── embedding: [0.1, ...]
├── Chunk 2
│   ├── docId: "doc_abc123"
│   ├── text: "In this chapter..."
│   └── embedding: [0.2, ...]
└── ...
```

**Why two separate stores?**
- Documents: Full text for display/re-processing
- Chunks: Searchable units with embeddings
- Different access patterns, different optimizations

---

### 2. Embedding Immutability

**Important:** Once generated, embeddings should NOT change!

```kotlin
// ✓ CORRECT: Same model every time
val embedding1 = modelV1.encode("Vector search")  // [0.1, 0.2, ...]
val embedding2 = modelV1.encode("Vector search")  // [0.1, 0.2, ...] ← Same!

// ✗ WRONG: Different models
val embedding1 = modelV1.encode("Vector search")  // [0.1, 0.2, ...]
val embedding2 = modelV2.encode("Vector search")  // [0.5, 0.7, ...] ← Different!
// Can't compare these! Must regenerate ALL embeddings if model changes!
```

---

### 3. Why Local Models?

**Cloud API (like Gemini Embeddings):**
- ❌ Cost per API call
- ❌ Network latency
- ❌ Privacy concerns (data leaves device)
- ❌ Requires internet

**Local ONNX Model:**
- ✅ Free (unlimited generations)
- ✅ Fast (10-50ms)
- ✅ Private (data stays on device)
- ✅ Works offline

**Trade-off:** Larger app size (~30MB for model file)

---

## 🐛 Common Student Issues

### Issue 1: Model File Missing

**Error:**
```
FileNotFoundException: all-MiniLM-L6-V2.onnx
```

**Fix:** Verify file exists:
`app/src/main/assets/all-MiniLM-L6-V2.onnx`

If missing, re-sync project or download from workshop repo.

---

### Issue 2: Wrong Embedding Dimensions

**Error in Exercise 4:**
```
Vector index expects 384 dimensions, got 512
```

**Cause:** Wrong ONNX model loaded

**Fix:** Ensure using `all-MiniLM-L6-V2` (outputs 384 dims)

---

### Issue 3: Forgetting to Link Chunks to Document

```kotlin
// ✗ WRONG: chunks are orphaned
val docId = documentsDB.addDocument(doc)
chunks.forEach { chunk ->
    chunksDB.addChunk(chunk)  // No docId!
}

// ✓ CORRECT: chunks reference parent
val docId = documentsDB.addDocument(doc)
chunks.forEach { chunk ->
    chunksUseCase.addChunk(
        docId = docId,  // Link to parent!
        docFileName = doc.docFileName,
        chunkText = chunk
    )
}
```

---

## 💡 Interactive Discussion Points

### Q1: "Why store full document AND chunks?"

**Answer:**
- **Full document:** For re-chunking if algorithm changes, showing complete context
- **Chunks:** For vector search (can't search entire 10K word document efficiently)
- **Analogy:** Like having both a book (full doc) and an index (chunks)

---

### Q2: "What if document is updated?"

**Answer:**
```kotlin
// When user updates document:
1. Delete old chunks: chunksDB.removeChunks(docId)
2. Update document: documentsDB.updateDocument(...)
3. Re-split into chunks
4. Re-generate embeddings (with same model!)
5. Save new chunks
```

---

### Q3: "How big can documents be?"

**Answer:**
- **Couchbase Lite:** No hard limit (tested with 100MB+ documents)
- **Practical limit:** App memory
- **Best practice:** 
  - Chunk documents as they're uploaded
  - Don't load entire 100MB document into memory
  - Stream and process in chunks

---

## 🎯 Success Criteria

Students have successfully completed Exercise 3 when:

✅ All three files modified (DocumentsDB, ChunksDB, ChunksUseCase)  
✅ Real implementations active (not placeholders)  
✅ Vector index creation still COMMENTED  
✅ Vector search still COMMENTED  
✅ App builds without errors  
✅ App runs without crashes  
✅ Database initialization logs still appear  
✅ Gemini test still works  

**Code review checklist:**
- `SentenceEmbeddingProvider` dependency added to `ChunksUseCase`
- `encodeText()` called in `addChunk()`
- Embedding stored as `MutableArray` with `addFloat()`
- `removeChunks()` uses `QueryBuilder` with `where` clause

---

## 🚀 Transition to Exercise 4

**Closing remarks:**
> "Perfect! We now have the complete storage layer. Documents and chunks are being saved with their embeddings. But we still can't SEARCH those embeddings. In Exercise 4, we'll activate vector search and complete the full RAG pipeline - allowing users to ask questions and get answers based on their uploaded documents!"

**Preview Exercise 4:**
- ✨ Create vector index for fast similarity search
- 🔍 Implement `getSimilarChunks()` with `APPROX_VECTOR_DISTANCE`
- 🤖 Wire up complete RAG: Retrieve → Augment → Generate
- 🎯 Test end-to-end: Upload PDF → Ask question → Get answer!

**Teaser:**
"Next exercise is the grand finale where everything comes together. You'll see the magic of RAG in action!"

---

## 📚 Additional Resources

- [ONNX Runtime for Android](https://onnxruntime.ai/docs/get-started/with-android.html)
- [all-MiniLM-L6-V2 Model Card](https://huggingface.co/sentence-transformers/all-MiniLM-L6-V2)
- [Couchbase QueryBuilder API](https://docs.couchbase.com/couchbase-lite/current/android/querybuilder.html)
- [Kotlin Flows Guide](https://kotlinlang.org/docs/flow.html)

---

## ⏱️ Time Management

- Embeddings review: 3 min
- DocumentsDB walkthrough: 8 min
- ChunksDB walkthrough: 8 min
- ChunksUseCase walkthrough: 7 min
- Build & verify: 2 min
- Q&A: 5 min
- **Total: ~33 min** (can compress by combining similar sections)

---

**Next File:** `Exercise4_SpeakerNotes.md`

