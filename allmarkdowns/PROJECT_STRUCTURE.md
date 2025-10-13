# Project Structure Guide - Couchbase Lite RAG Workshop

This document explains the complete architecture, directory structure, and design patterns used in the workshop application.

---

## 📁 High-Level Directory Structure

```
couchbase-lite-workshop/
├── app/
│   ├── build.gradle.kts              # App-level build configuration
│   ├── src/
│   │   └── main/
│   │       ├── AndroidManifest.xml   # App permissions & components
│   │       ├── assets/               # ONNX model files
│   │       │   ├── all-MiniLM-L6-V2.onnx
│   │       │   └── tokenizer.json
│   │       ├── java/com/ml/shivay_couchbase/docqa/
│   │       │   ├── DocQAApplication.kt
│   │       │   ├── MainActivity.kt
│   │       │   ├── data/             # Data layer
│   │       │   ├── domain/           # Business logic layer
│   │       │   ├── ui/               # Presentation layer
│   │       │   └── di/               # Dependency injection
│   │       └── res/                  # Android resources
├── build.gradle.kts                  # Project-level build config
├── gradle/
│   └── libs.versions.toml            # Dependency versions
├── local.properties                  # Local config (API keys)
└── sample_pdfs/                      # Test documents
```

---

## 🏗️ Architecture Overview

The app follows **Clean Architecture** principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────────┐
│                  UI Layer (Compose)                  │
│  ┌─────────────┐  ┌─────────────┐                  │
│  │ ChatScreen  │  │ DocsScreen  │                  │
│  └──────┬──────┘  └──────┬──────┘                  │
│         │                 │                          │
│         └────────┬────────┘                          │
├──────────────────┼──────────────────────────────────┤
│                  │  ViewModels                       │
│         ┌────────▼────────┐                         │
│         │  ChatViewModel  │                         │
│         │  DocsViewModel  │                         │
│         └────────┬────────┘                         │
├──────────────────┼──────────────────────────────────┤
│            Domain Layer (Use Cases)                  │
│         ┌────────▼────────┐                         │
│         │   QAUseCase     │◄───────────┐           │
│         │  ChunksUseCase  │            │           │
│         │ DocumentsUseCase│            │           │
│         └────────┬────────┘            │           │
├──────────────────┼─────────────────────┼───────────┤
│              Data Layer                 │           │
│         ┌────────▼────────┐    ┌───────▼──────┐   │
│         │   ChunksDB      │    │ GeminiRemoteAPI│  │
│         │  DocumentsDB    │    └────────────────┘  │
│         │ DatabaseManager │                        │
│         └────────┬────────┘                        │
├──────────────────┼──────────────────────────────────┤
│         External Dependencies                       │
│         ┌────────▼────────┐                        │
│         │ Couchbase Lite  │                        │
│         │ Vector Search   │                        │
│         │  ONNX Runtime   │                        │
│         └─────────────────┘                        │
└─────────────────────────────────────────────────────┘
```

---

## 📦 Detailed Package Structure

### `com.ml.couchbase.docqa` (Root Package)

#### **DocQAApplication.kt**
```kotlin
@HiltAndroidApp
class DocQAApplication : Application()
```
**Purpose:** Application entry point
- Initializes Hilt dependency injection
- First class instantiated when app starts
- Lives for entire app lifecycle

**Responsibilities:**
- None currently (Hilt handles initialization)
- Could add: Crash reporting, analytics, etc.

---

#### **MainActivity.kt**
```kotlin
@AndroidEntryPoint
class MainActivity : ComponentActivity()
```
**Purpose:** Single Activity hosting entire UI
- Sets up Jetpack Compose
- Handles navigation between screens
- Initializes database on startup

**Key Methods:**
- `onCreate()` - Initializes database, sets up UI
- Navigation: Chat screen ↔ Docs screen

---

### 📂 `data/` Package (Data Layer)

**Responsibility:** All data access and storage operations

---

#### **DatabaseManager.kt**
```kotlin
object DatabaseManager {
    private lateinit var database: Database
    fun init(context: Context)
    fun getDatabase(): Database
    fun isVectorSearchEnabled(): Boolean
}
```

**Purpose:** Singleton managing Couchbase Lite instance
- **Pattern:** Object (Kotlin singleton)
- **Lifecycle:** Lives for entire app

**Key Responsibilities:**
1. Initialize Couchbase Lite SDK
2. Enable Vector Search extension
3. Create/open database
4. Provide database instance to other classes

**Thread Safety:** Kotlin `object` is thread-safe by default

**When it runs:**
```
App Launch
    ↓
MainActivity.onCreate()
    ↓
DatabaseManager.init(applicationContext)  ← Happens here
    ↓
[Database ready for use]
```

---

#### **DataModels.kt**
```kotlin
data class Chunk(
    var chunkId: Long,
    var docId: String,
    var docFileName: String,
    var chunkData: String,
    var chunkEmbedding: FloatArray
)

data class Document(
    var docId: Long,
    var docText: String,
    var docFileName: String,
    var docAddedTime: Long
)

data class RetrievedContext(
    val fileName: String,
    val context: String
)

data class QueryResult(
    val response: String,
    val context: List<RetrievedContext>
)
```

**Purpose:** Data transfer objects (DTOs)

**Chunk:**
- Represents a piece of text with its embedding
- Links to parent document via `docId`
- Core unit for vector search

**Document:**
- Represents full document with metadata
- Parent of many chunks
- Used for display and re-processing

**RetrievedContext:**
- Chunk text + source file
- Used for citations in UI

**QueryResult:**
- LLM response + sources
- Returned to UI layer

---

#### **DocumentsDB.kt**
```kotlin
class DocumentsDB {
    fun addDocument(document: Document): String
    fun removeDocument(docId: String)
    fun getAllDocuments(): Flow<List<Document>>
    fun getDocsCount(): Long
}
```

**Purpose:** CRUD operations for full documents

**Responsibilities:**
- Store complete document text
- Query all documents (for UI list)
- Count documents (for empty state)
- Delete documents

**Pattern:** Repository pattern (abstraction over Couchbase)

**Data Flow:**
```
User uploads PDF
    ↓
PDFReader extracts text
    ↓
DocumentsDB.addDocument()  ← Saves full text
    ↓
Returns docId
    ↓
Used to link chunks
```

---

#### **ChunksDB.kt**
```kotlin
class ChunksDB {
    fun addChunk(chunk: Chunk)
    fun getSimilarChunks(queryEmbedding: FloatArray, n: Int): List<Pair<Float, Chunk>>
    fun removeChunks(docId: Long)
}
```

**Purpose:** Storage and retrieval of chunks with embeddings

**Responsibilities:**
1. Store chunks with 384-dim embeddings
2. Create vector index (IVF)
3. Perform vector similarity search
4. Delete chunks when document removed

**Key Feature:** Vector search using `APPROX_VECTOR_DISTANCE`

**Performance:** O(sqrt(n)) with index vs O(n) without

**Data Flow:**
```
Text chunk: "Vector search enables..."
    ↓
Generate embedding: [0.145, -0.023, ...]
    ↓
ChunksDB.addChunk()  ← Stores text + embedding
    ↓
Vector index auto-updates
    ↓
Ready for similarity search
```

---

### 📂 `domain/` Package (Business Logic Layer)

**Responsibility:** Business rules and use cases

---

#### **DocumentsUseCase.kt**
```kotlin
@Singleton
class DocumentsUseCase @Inject constructor(
    private val documentsDB: DocumentsDB
) {
    fun addDocument(...)
    fun removeDocument(...)
    fun getAllDocuments(): Flow<List<Document>>
    fun getDocsCount(): Long
}
```

**Purpose:** Business logic for document management
- Thin wrapper around DocumentsDB
- Future: Could add validation, transformation, etc.

**Pattern:** Use case pattern (single responsibility)

---

#### **ChunksUseCase.kt**
```kotlin
@Singleton
class ChunksUseCase @Inject constructor(
    private val chunksDB: ChunksDB,
    private val sentenceEncoder: SentenceEmbeddingProvider
) {
    fun addChunk(...)
    fun removeChunks(...)
    fun getSimilarChunks(query: String, n: Int): List<Pair<Float, Chunk>>
}
```

**Purpose:** Bridges text (UI) and vectors (DB)

**Key Responsibility:** Embedding generation
```kotlin
// Input: Text
val text = "Vector search is powerful"

// Process: Generate embedding
val embedding = sentenceEncoder.encodeText(text)
// → FloatArray[384]

// Output: Store with embedding
chunksDB.addChunk(Chunk(..., chunkEmbedding = embedding))
```

**Why this layer?**
- Database doesn't know about text → vector conversion
- UI doesn't know about ONNX models
- Use case connects them

---

#### **QAUseCase.kt**
```kotlin
@Singleton
class QAUseCase @Inject constructor(
    private val documentsUseCase: DocumentsUseCase,
    private val chunksUseCase: ChunksUseCase,
    private val geminiRemoteAPI: GeminiRemoteAPI
) {
    fun getAnswer(query: String, prompt: String, onResponse: (QueryResult) -> Unit)
    fun canGenerateAnswers(): Boolean
}
```

**Purpose:** THE RAG PIPELINE - Orchestrates entire Q&A flow

**The Complete Flow:**
```
1. RETRIEVAL
   chunksUseCase.getSimilarChunks(query)
   → Returns top 5 relevant chunks
   
2. AUGMENTATION
   Combine chunks into context
   Build prompt: "Context: ...\n\nQuestion: ..."
   
3. GENERATION
   geminiRemoteAPI.getResponse(prompt)
   → Returns grounded answer
   
4. RESULT
   onResponse(QueryResult(answer, sources))
   → Delivers to UI
```

**Thread Management:**
```
Starts on: Dispatchers.IO (background)
Ends on: Dispatchers.Main (UI)
```

---

### 📂 `domain/embeddings/` Package

#### **SentenceEmbeddingProvider.kt**
```kotlin
@Singleton
class SentenceEmbeddingProvider @Inject constructor() {
    fun encodeText(text: String): FloatArray
}
```

**Purpose:** Wrapper around ONNX Runtime

**Responsibilities:**
1. Load ONNX model from assets
2. Tokenize input text
3. Run inference
4. Return 384-dimensional embedding

**Model:** all-MiniLM-L6-V2
- Size: ~30 MB
- Speed: 10-50ms per encoding
- Dimensions: 384
- Language: Multilingual (100+ languages)

**Usage:**
```kotlin
val text = "What is vector search?"
val embedding = sentenceEncoder.encodeText(text)
// → FloatArray(384) { ... }
```

---

### 📂 `domain/llm/` Package

#### **GeminiRemoteAPI.kt**
```kotlin
class GeminiRemoteAPI {
    private val apiKey = BuildConfig.geminiKey
    private val generativeModel: GenerativeModel
    
    suspend fun getResponse(prompt: String): String?
}
```

**Purpose:** Interface to Google Gemini API

**Configuration:**
```kotlin
temperature = 0.3  // Low (factual)
topP = 0.4        // Focused
model = "gemini-2.5-flash"  // Fast & cheap
```

**API Call:**
```kotlin
// Input: Augmented prompt with context
val prompt = """
    Context: [Retrieved chunks]
    Question: What is vector search?
    Answer based on context only.
"""

// Network call (suspending)
val response = geminiRemoteAPI.getResponse(prompt)
// → "Vector search is a technique that..."
```

---

### 📂 `domain/readers/` Package

#### **Reader.kt** (Interface)
```kotlin
interface Reader {
    fun read(uri: Uri, context: Context): String
}
```

#### **PDFReader.kt** & **DOCXReader.kt**
```kotlin
class PDFReader : Reader {
    override fun read(uri: Uri, context: Context): String {
        // Uses iTextPDF to extract text
    }
}

class DOCXReader : Reader {
    override fun read(uri: Uri, context: Context): String {
        // Uses Apache POI to extract text
    }
}
```

**Purpose:** Extract text from different document formats

**Pattern:** Strategy pattern (different readers for different formats)

---

### 📂 `domain/splitters/` Package

#### **WhiteSpaceSplitter.kt**
```kotlin
object WhiteSpaceSplitter {
    fun split(text: String, chunkSize: Int = 500): List<String>
}
```

**Purpose:** Split document into chunks

**Algorithm:**
```kotlin
// Input: "This is a long document with many words..."
// chunkSize: 500 words

// Output:
[
    "This is a long document with many words...",  // 500 words
    "...continuing the document with more text...", // 500 words
    "...final part of the document."               // < 500 words
]
```

**Why chunking?**
- Can't search entire 10,000-word document
- Smaller chunks = more precise retrieval
- Balance: Too small = loses context, too large = imprecise

---

### 📂 `di/` Package (Dependency Injection)

#### **AppModule.kt**
```kotlin
@Module
@InstallIn(SingletonComponent::class)
object AppModule {
    @Provides
    @Singleton
    fun provideDocumentsDB(): DocumentsDB = DocumentsDB()
    
    @Provides
    @Singleton
    fun provideChunksDB(): ChunksDB = ChunksDB()
    
    @Provides
    @Singleton
    fun provideSentenceEmbeddingProvider(context: Context): SentenceEmbeddingProvider
        = SentenceEmbeddingProvider(context)
}
```

**Purpose:** Configure dependency injection with Hilt

**What it does:**
```
When class needs DocumentsDB:
    ↓
Hilt checks AppModule
    ↓
Finds @Provides fun provideDocumentsDB()
    ↓
Creates DocumentsDB instance (once, cached)
    ↓
Injects into requesting class
```

**Benefits:**
- No manual `new` calls
- Singleton pattern automatic
- Easy testing (mock injection)
- Clear dependency graph

---

### 📂 `ui/` Package (Presentation Layer)

#### **viewmodels/**

**ChatViewModel.kt**
```kotlin
@HiltViewModel
class ChatViewModel @Inject constructor(
    private val qaUseCase: QAUseCase
) : ViewModel() {
    
    val messages: StateFlow<List<Message>>
    
    fun sendMessage(query: String) {
        qaUseCase.getAnswer(query) { result ->
            // Update messages with result
        }
    }
}
```

**Purpose:** Manages chat screen state
- Holds message history
- Triggers Q&A pipeline
- Handles loading states

**DocsViewModel.kt**
```kotlin
@HiltViewModel
class DocsViewModel @Inject constructor(
    private val documentsUseCase: DocumentsUseCase,
    private val chunksUseCase: ChunksUseCase
) : ViewModel() {
    
    val documents: StateFlow<List<Document>>
    
    fun uploadDocument(uri: Uri) {
        // Read → Split → Embed → Save
    }
    
    fun deleteDocument(docId: String) {
        chunksUseCase.removeChunks(docId)
        documentsUseCase.removeDocument(docId)
    }
}
```

**Purpose:** Manages documents screen state
- Shows document list
- Handles upload/delete
- Processes documents (chunking, embedding)

---

#### **screens/**

**ChatScreen.kt** (Composable)
```kotlin
@Composable
fun ChatScreen(
    viewModel: ChatViewModel = hiltViewModel(),
    onOpenDocsClick: () -> Unit
) {
    val messages by viewModel.messages.collectAsState()
    
    // UI: Message list + input field
    LazyColumn {
        items(messages) { message ->
            MessageBubble(message)
        }
    }
    
    TextField(
        value = inputText,
        onValueChange = { inputText = it },
        onSend = { viewModel.sendMessage(inputText) }
    )
}
```

**DocsScreen.kt** (Composable)
```kotlin
@Composable
fun DocsScreen(
    viewModel: DocsViewModel = hiltViewModel(),
    onBackClick: () -> Unit
) {
    val documents by viewModel.documents.collectAsState()
    
    // UI: Document list + upload button
    LazyColumn {
        items(documents) { doc ->
            DocumentItem(
                document = doc,
                onDelete = { viewModel.deleteDocument(doc.id) }
            )
        }
    }
    
    FloatingActionButton(
        onClick = { /* Show file picker */ }
    )
}
```

---

## 🔄 Data Flow Examples

### Example 1: Uploading a Document

```
User selects PDF
    ↓
DocsViewModel.uploadDocument(uri)
    ↓
PDFReader.read(uri)
    ↓
Extracted text: "Full document text..."
    ↓
WhiteSpaceSplitter.split(text, 500)
    ↓
Chunks: ["Chunk 1...", "Chunk 2...", ...]
    ↓
documentsUseCase.addDocument(doc)
    ↓
DocumentsDB.addDocument()
    ↓
Couchbase: Document saved, returns docId
    ↓
For each chunk:
    chunksUseCase.addChunk(docId, chunk)
    ↓
    sentenceEncoder.encodeText(chunk)
    ↓
    embedding: FloatArray[384]
    ↓
    chunksDB.addChunk(Chunk with embedding)
    ↓
    Couchbase: Chunk saved
    ↓
    Vector index auto-updates
    ↓
Done! Document ready for search
```

---

### Example 2: Asking a Question (RAG)

```
User types: "What is vector search?"
    ↓
ChatViewModel.sendMessage(query)
    ↓
qaUseCase.getAnswer(query, prompt) { result ->
    [Background: Dispatchers.IO]
    
    ↓ RETRIEVAL
    chunksUseCase.getSimilarChunks(query, 5)
    ↓
    sentenceEncoder.encodeText(query)
    ↓
    queryEmbedding: FloatArray[384]
    ↓
    chunksDB.getSimilarChunks(queryEmbedding)
    ↓
    SQL: APPROX_VECTOR_DISTANCE(...)
    ↓
    Vector index search
    ↓
    Top 5 chunks: [Chunk, Chunk, Chunk, Chunk, Chunk]
    
    ↓ AUGMENTATION
    combine chunks → context string
    ↓
    prompt = "Context: [context]\n\nQuestion: [query]"
    
    ↓ GENERATION
    geminiRemoteAPI.getResponse(prompt)
    ↓
    HTTP POST to Gemini API
    ↓
    Wait 1-3 seconds...
    ↓
    LLM response received
    
    ↓ THREAD SWITCH
    CoroutineScope(Dispatchers.Main).launch {
        [Now on Main Thread]
        ↓
        onResponse(QueryResult(answer, sources))
    }
}
    ↓
ChatViewModel updates message list
    ↓
UI recomposes
    ↓
User sees answer!
```

---

## 🧩 Design Patterns Used

### 1. **Clean Architecture**
- Clear layer separation (UI → Domain → Data)
- Dependencies point inward
- Business logic independent of frameworks

### 2. **Repository Pattern**
- `DocumentsDB`, `ChunksDB` abstract data access
- Could swap Couchbase for Room/Realm/etc.

### 3. **Use Case Pattern**
- Each use case = single business operation
- `QAUseCase`, `ChunksUseCase`, `DocumentsUseCase`

### 4. **Singleton Pattern**
- `DatabaseManager`, Use Cases
- Single instance for app lifecycle

### 5. **Strategy Pattern**
- `Reader` interface with `PDFReader`, `DOCXReader`
- Different algorithms for different formats

### 6. **Observer Pattern (via Flow)**
- ViewModels expose `StateFlow`
- UI observes and reacts to changes

### 7. **Dependency Injection**
- Hilt manages object creation
- Loose coupling between components

---

## 🔐 Security Considerations

### API Key Storage
```
local.properties (NOT in Git)
    ↓
Gradle reads at build time
    ↓
BuildConfig.geminiKey (compile-time constant)
    ↓
Used in GeminiRemoteAPI
```

**Important:** Never commit `local.properties`!

---

## 📊 Threading Model

```
Main Thread (UI):
- User interactions
- Compose recomposition
- View updates

Dispatchers.IO:
- Database operations
- Network calls
- File I/O
- Embedding generation

Dispatchers.Default:
- CPU-intensive work
- Not used in this app (could be for large computations)
```

---

## 🎯 Key Architectural Decisions

### Why Couchbase Lite?
- ✅ Offline-first (works without internet)
- ✅ Native vector search support
- ✅ Embedded database (no server)
- ✅ Fast queries with indexing
- ✅ ACID transactions

### Why Local ONNX Model?
- ✅ Free (no API costs)
- ✅ Fast (10-50ms)
- ✅ Private (data doesn't leave device)
- ✅ Offline (works without internet)

### Why Jetpack Compose?
- ✅ Modern declarative UI
- ✅ Less boilerplate
- ✅ Reactive by default
- ✅ Better performance

### Why Hilt?
- ✅ Compile-time DI (fast)
- ✅ Android-specific
- ✅ Less boilerplate than Dagger
- ✅ Lifecycle-aware

---

## 📈 Performance Characteristics

| Operation | Time | Notes |
|-----------|------|-------|
| App startup | 500ms | Database init + Hilt |
| Generate embedding | 10-50ms | ONNX inference |
| Vector search (1K chunks) | 20-50ms | With index |
| Vector search (10K chunks) | 50-100ms | With index |
| LLM API call | 1-3s | Network dependent |
| Full RAG pipeline | 1.5-3.5s | Retrieval + LLM |

---

## 🧪 Testing Strategy

### Unit Tests (Should Add)
- Use cases with mocked repositories
- Embedding generation
- Text splitting logic

### Integration Tests
- Database operations
- RAG pipeline end-to-end

### UI Tests
- Compose UI tests
- User flow testing

---

## 🚀 Scalability Considerations

### Current Limits:
- ~10,000 chunks: Fast
- ~100,000 chunks: Still OK
- > 1M chunks: Need optimization

### Optimization Strategies:
1. **Increase index centroids:** 3 → 10
2. **Use quantized embeddings:** 4x smaller
3. **Batch operations:** Bulk inserts
4. **Lazy loading:** Don't load all docs at once
5. **Index tuning:** Experiment with parameters

---

This structure provides a solid foundation for building production RAG applications!

