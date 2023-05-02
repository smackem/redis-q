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
    | pipelineExpr '|>' '{' pipelineRhsExpr '}'
    | expr
    ;

pipelineRhsExpr
    : expr
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
    : limitClauseLimitPart limitClauseOffsetPart
    | limitClauseOffsetPart
    | limitClauseLimitPart
    ;

limitClauseLimitPart
    : Limit ternaryExpr
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
    : compositionalExpr
    | compositionalExpr relationalOp compositionalExpr
    ;

relationalOp
    : Eq
    | Ne
    | Lt
    | Le
    | Gt
    | Ge
    | RegexMatch
    | NotRegexMatch
    | NullCoalesce
    ;

compositionalExpr
    : additiveExpr
    | compositionalExpr compositionalOp additiveExpr
    ;

compositionalOp
    : FromTo
    | With
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
    : '[' pipelineExpr ']'
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
    | PipelineValue
    ;

tuple
    : '(' tupleItem ',' tupleItem (',' tupleItem)* ')'
    | '(' Ident ':' pipelineExpr ')'
    ;

tupleItem
    : (Ident ':')? pipelineExpr
    ;

list
    : '[' arguments? ']'
    ;

functionInvocation
    : Ident '(' arguments? ')'
    ;

arguments
    : pipelineExpr (',' pipelineExpr)*
    ;

number
    : (Plus | Minus)? (Integer | Real | HexInteger | BinaryInteger)
    ;

string
    : DoubleQuotedString
    | SingleQuotedString
    ;

throwExpr
    : Throw expr
    ;

From            : 'from';
In              : 'in';
Let             : 'let';
Where           : 'where';
Limit           : 'limit';
Offset          : 'offset';
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
Throw           : 'throw';
With            : 'with';
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
RegexMatch      : '=~';
NotRegexMatch   : '!~';
Or              : '||';
And             : '&&';
Not             : '!';
FromTo          : '..';
NullCoalesce    : '??';
PipelineValue   : '$';

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