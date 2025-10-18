🧠 RedLang Interpreter (C# + ANTLR4)

A lightweight recursive interpreter implemented in C#, powered by ANTLR4 for parsing and grammar generation.
Designed to support typed variables, control flow, recursion, and I/O evaluation within a minimal syntax.

🚀 Features

🧩 Custom Grammar written in ANTLR4 (RedLang.g4 + ExprLexer.g4)
🧮 Arithmetic and logic expressions: + - * / % && || == != < > <= >=
📦 Typed variables: (int, float, bool, s) with dynamic coercion

🔁 Control structures:

check(cond){...} otherwise {...} — conditional block

loop(init; cond; step){...} — for-style loop

🧠 Recursive function support
💬 I/O statements:

show(expr) — print to output

ask(x) — request user input

🎯 Return mechanism via give expr
🔄 Full recursive evaluation using a custom EvalVisitor





👌 Example Programs

func factorial(n:int):int{check(n<=1){give 1}give n*factorial(n-1)} 
show(factorial(5))

func fib(n:int):int{check(n<=1){give n}give fib(n-1)+fib(n-2)} 
show(fib(8))

func pow(a:int,b:int):int{check(b==0){give 1}give a*pow(a,b-1)} 
show(pow(3,4))




👌 Example programs:
func factorial(n:int):int{check(n<=1){give 1}give n*factorial(n-1)} 
show(factorial(5))

func fib(n:int):int{check(n<=1){give n}give fib(n-1)+fib(n-2)} 
show(fib(8))

func pow(a:int,b:int):int{check(b==0){give 1}give a*pow(a,b-1)} 
show(pow(3,4))




🛠️ Build Instructions

1️⃣ Requirements
.NET 8 SDK
ANTLR 4.13.2
Java 17+ installed and in PATH

2️⃣ Generate Parser and Lexer
Inside the grammar folder:
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor ExprLexer.g4 RedLang.g4 -o Generated

This will create:
Generated/
 ├─ ExprLexer.cs
 
 ├─ RedLangParser.cs
 ├─ RedLangBaseVisitor.cs
 ├─ RedLangVisitor.cs





🧩 Project Structure
├── Grammar/
│   ├── ExprLexer.g4       # Lexer rules
│   ├── RedLang.g4         # Parser rules
│
├── Interpreter/
│   ├── EvalVisitor.cs     # Execution logic (C# runtime)
│   ├── Program.cs         # Entry point / REPL
│
├── Examples/
│   ├── factorial.red      # Example RedLang script
│
└── README.md





📝Implementation Notes

The interpreter uses nested scopes (stack-based symbol table).

Functions are stored globally in _funcs and support recursion via pre-registration.

give triggers a ReturnSignal exception internally to unwind function calls safely.

Expression evaluation supports mixed-type coercion (int, float, bool, string).




✍️ Author
Randy Made
💻 Compiler & Software Engineering Enthusiast
📍 Dominican Republic
