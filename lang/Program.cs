using Antlr4.Runtime;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("RedLang REPL. Escribe una expresión y presiona Enter. Ctrl+C para salir.");
        var visitor = new EvalVisitor();
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input == null) break; // EOF -> exit
            input = input.Trim();
            if (input.Length == 0) continue;
            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase)) break;
            try
            {
                var inputStream = new AntlrInputStream(input);
                var lexer = new ExprLexer(inputStream);
                var tokens = new CommonTokenStream(lexer);
                var parser = new RedLang(tokens);
                var tree = parser.statement();
                var result = visitor.Visit(tree);
                Console.WriteLine($"= {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}