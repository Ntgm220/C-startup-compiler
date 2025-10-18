parser grammar RedLang;
options { tokenVocab=ExprLexer; }

// Entry point
program
    : statement* EOF
    ;

// Expressions
expression  : logicOr ;
logicOr     : logicAnd (OR logicAnd)* ;
logicAnd    : equality (AND equality)* ;
equality    : comparison ((EQEQ | NOTEQ) comparison)* ;
comparison  : term ((GT | LT | GTEQ | LTEQ) term)* ;
term        : factor ((PLUS | MINUS) factor)* ;
factor      : unary ((STAR | SLASH | PERCENT) unary)* ;
unary       : (MINUS | NOT)? primary ;
primary     : literal
            | functionCall
            | LPAREN expression RPAREN
            | IDENTIFIER
            ;
literal     : INT_LIT
            | FLOAT_LIT
            | TRUE
            | FALSE
            | STRING_LIT
            ;

// Statements
statement
    : varDef
    | assignment
    | functionDef
    | loopStmt
    | ifStmt
    | showStmt
    | askStmt
    | giveStmt
    | functionCallStmt
    | block
    | expression
    ;

varDef              : DECLARE IDENTIFIER COLON type (EQ expression)? ;
assignment          : SET IDENTIFIER EQ expression ;
functionDef         : FUNC IDENTIFIER LPAREN paramList? RPAREN COLON type block ;
paramList           : param (COMMA param)* ;
param               : IDENTIFIER COLON type ;
functionCall        : IDENTIFIER LPAREN argList? RPAREN ;
functionCallStmt    : functionCall ;
argList             : expression (COMMA expression)* ;
block               : LBRACE statement* RBRACE ;
loopStmt            : LOOP LPAREN statement expression SEMICOLON statement RPAREN block ;
ifStmt              : CHECK LPAREN expression RPAREN block (OTHERWISE block)? ;
showStmt            : SHOW LPAREN expression RPAREN ;
askStmt             : ASK LPAREN IDENTIFIER RPAREN ;
giveStmt            : GIVE expression ;
type                : INT | FLOAT | BOOL | S ;
