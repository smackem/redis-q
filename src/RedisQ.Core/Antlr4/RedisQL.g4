grammar RedisQL;

main
    : (pipelineExpr
    | letClause
    | funcBinding) EOF
    ;

funcBinding
    : Let Ident '(' identList? ')' '=' pipelineExpr
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
    : Limit (ternaryExpr | All) (Offset ternaryExpr)?
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
    ;

multiplicativeExpr
    : unaryExpr
    | multiplicativeExpr multiplicativeOp unaryExpr
    ;

multiplicativeOp
    : Times
    | Div
    | Mod
    ;

unaryExpr
    : unaryOp unaryExpr
    | postFixedPrimary
    ;

unaryOp
    : Minus
    | Plus
    | Not
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
    | '(' pipelineExpr ')'
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
    : (Plus | Minus)? (Integer | Real | HexInteger)
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

DoubleQuotedString
    : '"' ~["\r\n]* '"'
    ;

SingleQuotedString
    : '\'' ~['\r\n]* '\''
    ;

Comment
    : '//' ~ [\r\n]* -> skip
    ;

Ws
    : [ \t\r\n] -> skip
    ;