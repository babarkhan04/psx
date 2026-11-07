import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const source=await readFile(new URL('../../src/SmartPOS.Web/wwwroot/pos-keyboard.js',import.meta.url),'utf8');
const keyboard=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
function setup(dialogs=[]){
 const calls=[];let handler;
 globalThis.document={querySelectorAll:()=>dialogs,addEventListener:(_,h)=>{handler=h},removeEventListener:()=>{handler=undefined}};
 keyboard.register({invokeMethodAsync:method=>{calls.push(method);return Promise.resolve()}},[]);
 const key=(key='F4',typing=false)=>{const e={key,target:{matches:()=>typing},ctrlKey:false,altKey:false,shiftKey:false,preventDefault(){this.prevented=true},stopPropagation(){this.stopped=true}};handler?.(e);return e};
 return{calls,key,disposed:()=>handler===undefined};
}
test('F4 opens customer flow even while typing a barcode',()=>{const s=setup();const e=s.key('F4',true);assert.deepEqual(s.calls,['TriggerCustomerSelect']);assert.equal(e.prevented,true);keyboard.dispose()});
test('F4 in payment opens its customer action without invoking page checkout shortcuts',()=>{let clicked=0;const s=setup([{querySelector:q=>q.includes('action')?{disabled:false,click:()=>clicked++}:null}]);s.key();assert.equal(clicked,1);assert.deepEqual(s.calls,[]);keyboard.dispose()});
test('F4 in customer form focuses and selects phone lookup',()=>{let focused=0,selected=0;const s=setup([{querySelector:q=>q.includes('search')?{focus:()=>focused++,select:()=>selected++}:null}]);s.key();assert.equal(focused,1);assert.equal(selected,1);assert.deepEqual(s.calls,[]);keyboard.dispose()});
test('F4 respects top receipt popup and does not reopen a lower customer dialog',()=>{let clicks=0;const s=setup([{querySelector:()=>({disabled:false,click:()=>clicks++})},{querySelector:()=>null}]);s.key();assert.equal(clicks,0);assert.deepEqual(s.calls,[]);keyboard.dispose()});
test('payment shortcut is paused while editing customer details',()=>{const s=setup([{querySelector:()=>null}]);s.key('F10');assert.deepEqual(s.calls,[]);keyboard.dispose()});
test('disabled customer action cannot be invoked through F4',()=>{let clicks=0;const s=setup([{querySelector:q=>q.includes('action')?{disabled:true,click:()=>clicks++}:null}]);s.key();assert.equal(clicks,0);assert.deepEqual(s.calls,[]);keyboard.dispose()});
test('disposing the POS removes the keyboard handler',()=>{const s=setup();keyboard.dispose();assert.equal(s.disposed(),true);s.key();assert.deepEqual(s.calls,[])});
