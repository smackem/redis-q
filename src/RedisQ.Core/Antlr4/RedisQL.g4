grammar RedisQL;

main
    : (expr
    | letClause) EOF
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
    ;

fromClause
    : 'from' Ident 'in' primary
    ;

letClause
    : 'let' Ident '=' expr
    ;

whereClause
    : 'where' ternaryExpr
    ;

selectClause
    : 'select' expr
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
    : additiveExpr
    | additiveExpr relationalOp additiveExpr
    ;

relationalOp
    : Eq
    | Ne
    | Lt
    | Le
    | Gt
    | Ge
    | Match
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
    | postFixedPrimary indirectFieldAccessPostfix
    | postFixedPrimary subscriptPostfix
    ;

fieldAccessPostfix
    : '.' Ident
    ;

indirectFieldAccessPostfix
    : '->' Ident
    ;

subscriptPostfix
    : '[' expr ']'
    ;

primary
    : Ident
    | number
    | StringLiteral
    | CharLiteral
    | True
    | False
    | Null
    | throwExpr
    | functionInvocation
    | tuple
    | list
    | '(' expr ')'
    ;

tuple
    : '(' expr ',' expr (',' expr)* ')'
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

throwExpr
    : 'throw' expr
    ;

Plus        : '+';
Minus       : '-';
Times       : '*';
Div         : '/';
Mod         : '%';
Lt          : '<';
Le          : '<=';
Gt          : '>';
Ge          : '>=';
Eq          : '==';
Ne          : '!=';
Match       : '~=';
Or          : '||';
And         : '&&';
Not         : '!';
True        : 'true';
False       : 'false';
Null        : 'null';

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

StringLiteral
    : '"' ~["\\\r\n]* '"'
    ;

CharLiteral
    : '\'' ~['\\\r\n] '\''
    ;

Comment
    : '//' ~ [\r\n]* -> skip
    ;

Ws
    : [ \t\r\n] -> skip
    ;