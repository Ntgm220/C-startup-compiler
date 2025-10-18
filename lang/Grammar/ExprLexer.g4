lexer grammar ExprLexer;

// Palabras clave
FUNC        : 'func';
DECLARE     : 'declare';
SET         : 'set';
GIVE        : 'give';
SHOW        : 'show';
ASK         : 'ask';
LOOP        : 'loop';
CHECK       : 'check';
OTHERWISE   : 'otherwise';

// Tipos
INT         : 'int';
FLOAT       : 'float';
BOOL        : 'bool';
S           : 's';

// Símbolos
LPAREN      : '(';
RPAREN      : ')';
LBRACE      : '{';
RBRACE      : '}';
COLON       : ':';
COMMA       : ',';
EQ          : '=';
SEMICOLON   : ';';
PLUS        : '+';
MINUS       : '-';
STAR        : '*';
SLASH       : '/';
PERCENT     : '%';
LT          : '<';
GT          : '>';
LTEQ        : '<=';
GTEQ        : '>=';
EQEQ        : '==';
NOTEQ       : '!=';
NOT         : '!';
AND         : '&&';
OR          : '||';

// Literales
INT_LIT     : [0-9]+;
FLOAT_LIT   : [0-9]+'.'[0-9]+;
TRUE        : 'true';
FALSE       : 'false';
STRING_LIT  : '"' (~["\\] | '\\' .)* '"';
IDENTIFIER  : [a-zA-Z_][a-zA-Z_0-9]*;

// Espacios
WS          : [ \t\r\n]+ -> skip;
