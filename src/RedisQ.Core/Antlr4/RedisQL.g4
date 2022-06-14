grammar RedisQL;

main
    : (mainExpr ';')* EOF
    ;

mainExpr
    : letClause
    | letExpr
    | funcBinding
    ;

letExpr
    : letClause 'in' letExpr?
    | pipelineExpr
    ;

funcBinding
    : Let Ident '(' identList? ')' '=' letExpr
    ;

identList
    : Ident (',' Ident)*
    ;

pipelineExpr
    : pipelineExpr '|>' functionInvocation
    | expr
    ;

expr
    : ternaryExpr
    | fromExpr
    ; 

fromExpr
    : fromClause nestedClause* selectClause
    ;

nestedClause
    : fromClause
    | letClause
    | whereClause
    | limitClause
    | orderByClause
    | groupByClause
    ;

fromClause
    : From Ident In ternaryExpr
    ;

letClause
    : Let Ident '=' pipelineExpr
    ;

whereClause
    : Where ternaryExpr
    ;

limitClause
    : limitClauseLimitPart limitClauseOffsetPart?
    ;

limitClauseLimitPart
    : Limit (ternaryExpr | All)
    ;

limitClauseOffsetPart
    : Offset ternaryExpr
    ;

orderByClause
    : OrderBy ternaryExpr (Descending | Ascending)?
    ;

groupByClause
    : Group ternaryExpr By ternaryExpr Into Ident
    ;

selectClause
    : Select expr
    ;

ternaryExpr
    : conditionalOrExpr
    | conditionalOrExpr '?' conditionalOrExpr ':' ternaryExpr
    ;

conditionalOrExpr
    : conditionalAndExpr
    | conditionalAndExpr Or conditionalOrExpr
    ;

conditionalAndExpr
    : relationalExpr
    | relationalExpr And conditionalAndExpr
    ;

relationalExpr
    : rangeExpr
    | rangeExpr relationalOp rangeExpr
    ;

relationalOp
    : Eq
    | Ne
    | Lt
    | Le
    | Gt
    | Ge
    | RegexMatch
    | NullCoalesce
    ;

rangeExpr
    : additiveExpr
    | additiveExpr FromTo additiveExpr
    ;

additiveExpr
    : multiplicativeExpr
    | additiveExpr additiveOp multiplicativeExpr
    ;

additiveOp
    : Plus
    | Minus
    | BitAnd
    | BitOr
    | BitXor
    ;

multiplicativeExpr
    : unaryExpr
    | multiplicativeExpr multiplicativeOp unaryExpr
    ;

multiplicativeOp
    : Times
    | Div
    | Mod
    | BitLShift
    | BitRShift
    ;

unaryExpr
    : unaryOp unaryExpr
    | postFixedPrimary
    ;

unaryOp
    : Minus
    | Plus
    | Not
    | BitNot
    ;

postFixedPrimary
    : primary
    | postFixedPrimary fieldAccessPostfix
    | postFixedPrimary subscriptPostfix
    ;

fieldAccessPostfix
    : '.' Ident
    ;

subscriptPostfix
    : '[' expr ']'
    ;

primary
    : Ident
    | number
    | string
    | True
    | False
    | Null
    | throwExpr
    | functionInvocation
    | tuple
    | list
    | '(' letExpr ')'
    ;

tuple
    : '(' tupleItem ',' tupleItem (',' tupleItem)* ')'
    ;

tupleItem
    : (Ident ':')? expr
    ;

list
    : '[' arguments? ']'
    ;

functionInvocation
    : Ident '(' arguments? ')'
    ;

arguments
    : expr (',' expr)*
    ;

number
    : (Plus | Minus)? (Integer | Real | HexInteger | BinaryInteger)
    ;

string
    : DoubleQuotedString
    | SingleQuotedString
    ;

throwExpr
    : 'throw' expr
    ;

From            : 'from';
In              : 'in';
Let             : 'let';
Where           : 'where';
Limit           : 'limit';
Offset          : 'offset';
All             : 'all';
Group           : 'group';
By              : 'by';
Into            : 'into';
Select          : 'select';
True            : 'true';
False           : 'false';
Null            : 'null';
OrderBy         : 'orderby';
Descending      : 'descending' | 'desc';
Ascending       : 'ascending';
Plus            : '+';
Minus           : '-';
Times           : '*';
Div             : '/';
Mod             : '%';
BitAnd          : '&';
BitOr           : '|';
BitXor          : '^';
BitLShift       : '<<';
BitRShift       : '>>';
BitNot          : '~';
Lt              : '<';
Le              : '<=';
Gt              : '>';
Ge              : '>=';
Eq              : '==';
Ne              : '!=';
RegexMatch      : '~=';
Or              : '||';
And             : '&&';
Not             : '!';
FromTo          : '..';
NullCoalesce    : '??';

Ident
    : ([a-z] | [A-Z] | '_') ([a-z] | [A-Z] | DigitOrUnderscore)*
    ;

Integer
    : Digit DigitOrUnderscore*
    ;

Digit
    : [0-9]
    ;

DigitOrUnderscore
    : Digit | '_'
    ;

Real
    : Digit DigitOrUnderscore* '.' Digit DigitOrUnderscore*
    ;

HexInteger
    : '0x' HexLiteral+
    ;

HexLiteral
    : ([a-f] | [A-F] | DigitOrUnderscore)
    ;

BinaryInteger
    : '0b' BinaryLiteral+
    ;

BinaryLiteral
    : '0' | '1' | '_'
    ;

DoubleQuotedString
    : '"' ~["\r\n]* '"'
    ;

SingleQuotedString
    : '\'' ~['\r\n]* '\''
    ;

// unreferenced rule for lexing while typing
UnterminatedString
    : '\'' ~['\r\n]*
    | '"' ~["\r\n]*
    ;

Comment
    : '//' ~ [\r\n]* -> skip
    ;

Ws
    : [ \t\r\n] -> skip
    ;