grammar RedisQL;

main
    : expr EOF
    ;

expr
    : conditionalOrExpr
    | fromClause (fromClause | letClause | whereClause)* selectClause
    ; 

fromClause
    : 'from' Ident 'in' primary
    ;

letClause
    : 'let' Ident '=' conditionalOrExpr
    ;

whereClause
    : 'where' conditionalOrExpr
    ;

selectClause
    : 'select' conditionalOrExpr
    ;

conditionalOrExpr
    : conditionalAndExpr
    | conditionalAndExpr Or conditionalOrExpr
    ;

conditionalAndExpr
    : relationalExpr
    | relationalExpr And relationalExpr
    ;

relationalExpr
    : additiveExpr
    | additiveExpr relationalOp additiveExpr?
    ;

relationalOp
    : Eq
    | Lt
    | Le
    | Gt
    | Ge
    | Ne
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
    | postFixedPrimary mapAccessPostfix
    ;

fieldAccessPostfix
    : '.' Ident
    ;

indirectFieldAccessPostfix
    : '->' Ident
    ;

mapAccessPostfix
    : '[' expr ']'
    ;

primary
    : Ident
    | number
    | StringLiteral
    | CharLiteral
    | functionInvocation
    | '(' expr ')'
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

Plus        : '+';
Minus       : '-';
Times       : '*';
Div         : '/';
Mod         : '%';
Bor         : '|';
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